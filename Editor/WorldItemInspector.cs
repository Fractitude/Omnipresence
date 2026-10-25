namespace Omnipresence.Editor;

/// <summary>
/// The Inspector for <see cref="WorldItem"/>. An item placed by hand is edited here and nowhere else: a type picker on top
/// (every kind of the <see cref="ItemCatalog"/> that can be placed, by its display name, then "Custom item"), and below it
/// only the fields that kind declares (<see cref="ItemAuthoringRules.FieldsOf"/>): cash has an amount, a drug's items a
/// count up to the stack size and a quality, a custom item a name and a description. Which fields a kind has is data on
/// the kind (<see cref="ItemDef.Fields"/>), not a list in this file, so a new kind needs no change here. A live line says
/// what the player will see, and what is wrong is shown in red.
/// </summary>
/// <remarks>
/// Every change goes through <see cref="SerializedObject"/> (start edit, set, changed, finish edit) so the editor records
/// it for undo and redo, and the panel notices a change it did not make (an undo) by comparing the values every frame.
/// With several items selected it only says so; an item the running game made (dropped by a player) is shown, not edited.
/// </remarks>
[CustomEditor( typeof( WorldItem ) )]
public sealed class WorldItemInspector : ComponentEditorWidget
{
	WorldItem item;
	SerializedProperty authoring;
	SerializedObject authoringObject;
	Widget body;
	Label preview;
	Label problems;
	Label stackLabel;
	QualityBar bar;
	string signature = "";
	bool building;

	public WorldItemInspector( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 6;
		Layout.Margin = new Sandbox.UI.Margin( 0, 4, 0, 8 );

		Build();
	}

	/// <summary>
	/// The authoring values as the component has them right now.
	/// </summary>
	ItemAuthoring Values => item.IsValid() ? item.Authoring ?? new() : new();

	static string Sign( ItemAuthoring a ) => $"{a.Kind}|{a.Count}|{a.Quality}|{a.Amount}|{a.Name}|{a.Description}";

	void Build()
	{
		Layout.Clear( true );
		body = null;
		item = SerializedObject.Targets.FirstOrDefault() as WorldItem;

		if ( !item.IsValid() )
			return;

		if ( SerializedObject.IsMultipleTargets )
		{
			Layout.Add( Note( "Several items are selected. Select one item to choose what it is.", Dim ) );
			return;
		}

		// An item the running game made (a drop) is data from the game: shown, never edited.
		if ( item.Data is { } made )
		{
			Layout.Add( Note( $"Made by the game: {Describe( made )}", Dim ) );
			return;
		}

		// A component saved before the knobs moved here: carry its values over.
		item.MigrateLegacy();

		authoring = SerializedObject.GetProperty( nameof( WorldItem.Authoring ) );
		authoring.TryGetAsObject( out authoringObject );

		Layout.Add( Note( "ITEM TYPE", Dim, 8 ) );
		Layout.Add( BuildPicker() );

		body = Layout.Add( new Widget( this ) { Layout = Layout.Column() } );
		body.Layout.Spacing = 6;
		RebuildFields();

		Layout.AddStretchCell();
	}

	// -- The type picker ----------------------------------------------------------------------------------------------

	Widget BuildPicker()
	{
		var picker = new ComboBox( this );
		var choices = ItemAuthoringRules.Choices();
		var current = Values.Kind ?? "";

		building = true;

		foreach ( var choice in choices )
		{
			var kind = choice.Kind;
			picker.AddItem( choice.Label, choice.Icon, () => ChangeKind( kind ) );
		}

		var index = choices.FindIndex( x => x.Kind == current );
		picker.CurrentIndex = index < 0 ? choices.Count - 1 : index;

		building = false;
		return picker;
	}

	/// <summary>
	/// The author chose another type: the kind changes and every value that type does not use goes back to its default, so
	/// nothing stale is saved. The fields below are rebuilt for the new type.
	/// </summary>
	void ChangeKind( string kind )
	{
		if ( building || !item.IsValid() || (Values.Kind ?? "") == kind )
			return;

		var next = ItemAuthoringRules.Normalise( new ItemAuthoring { Kind = kind } );

		Edit( () =>
		{
			Set( nameof( ItemAuthoring.Kind ), next.Kind );
			Set( nameof( ItemAuthoring.Count ), next.Count );
			Set( nameof( ItemAuthoring.Quality ), next.Quality );
			Set( nameof( ItemAuthoring.Amount ), next.Amount );
			Set( nameof( ItemAuthoring.Name ), next.Name );
			Set( nameof( ItemAuthoring.Description ), next.Description );
		} );

		RebuildFields();
	}

	// -- The fields of the chosen type ---------------------------------------------------------------------------------

	void RebuildFields()
	{
		if ( body is null )
			return;

		body.Layout.Clear( true );
		stackLabel = null;
		bar = null;

		var a = Values;
		var fields = ItemAuthoringRules.FieldsOf( a.Kind );

		if ( ItemCatalog.Find( a.Kind ) is { } def )
			body.Layout.Add( Note( def.Description, Dim ) );
		else
			body.Layout.Add( Note( "An ordinary item with a name of your own.", Dim ) );

		if ( fields.HasFlag( ItemField.Count ) )
			body.Layout.Add( CountRow( a ) );

		if ( fields.HasFlag( ItemField.Quality ) )
			body.Layout.Add( QualityRow( a ) );

		if ( fields.HasFlag( ItemField.Amount ) )
			body.Layout.Add( AmountRow( a ) );

		if ( fields.HasFlag( ItemField.Name ) )
			body.Layout.Add( TextRow( "Name", a.Name, nameof( ItemAuthoring.Name ),
				string.IsNullOrEmpty( a.Kind ) ? "Required" : "Replaces the kind's name" ) );

		if ( fields.HasFlag( ItemField.Description ) )
			body.Layout.Add( TextRow( "Description", a.Description, nameof( ItemAuthoring.Description ), "Optional" ) );

		body.Layout.Add( Note( "WHAT THE PLAYER SEES", Dim, 8 ) );
		preview = body.Layout.Add( Note( "", Bright, 11 ) );
		problems = body.Layout.Add( Note( "", Bad ) );
		UpdatePreview();

		signature = Sign( Values );
	}

	Widget CountRow( ItemAuthoring a )
	{
		var max = ItemAuthoringRules.MaxCount( a.Kind );
		var slider = new FloatSlider( this ) { Minimum = 1, Maximum = max, Step = 1, Value = System.Math.Clamp( a.Count, 1, max ) };
		stackLabel = new Label( StackText( a.Count, max ), this );

		slider.OnValueEdited = () =>
		{
			var count = (int)System.MathF.Round( slider.Value );
			Edit( () => Set( nameof( ItemAuthoring.Count ), count ) );
			stackLabel.Text = StackText( count, max );
			Changed();
		};

		return Row( "Count", slider, stackLabel );
	}

	Widget QualityRow( ItemAuthoring a )
	{
		var slider = new FloatSlider( this ) { Minimum = 0, Maximum = 100, Step = 1, Value = System.Math.Clamp( a.Quality, 0, 100 ) };
		bar = new QualityBar( this ) { Value = a.Quality };

		slider.OnValueEdited = () =>
		{
			var quality = (int)System.MathF.Round( slider.Value );
			Edit( () => Set( nameof( ItemAuthoring.Quality ), quality ) );
			bar.Value = quality;
			bar.Update();
			Changed();
		};

		return Row( "Quality", slider, bar );
	}

	Widget AmountRow( ItemAuthoring a )
	{
		var edit = new LineEdit( this ) { Text = Money( a.Amount ), PlaceholderText = "Dollars" };

		void Commit( long amount )
		{
			amount = System.Math.Clamp( amount, ItemAuthoringRules.MinAmount, ItemAuthoringRules.MaxAmount );
			Edit( () => Set( nameof( ItemAuthoring.Amount ), amount ) );
			edit.Text = Money( amount );
			Changed();
		}

		edit.EditingFinished += () => Commit( ParseMoney( edit.Text, Values.Amount ) );

		var host = new Widget( this ) { Layout = Layout.Column() };
		host.Layout.Spacing = 4;
		host.Layout.Add( edit );

		var quick = host.Layout.AddRow();
		quick.Spacing = 4;

		foreach ( var amount in new long[] { 100, 1_000, 10_000, 100_000 } )
		{
			var value = amount;
			quick.Add( new Button( Money( value ), this ) { Clicked = () => Commit( value ) } );
		}

		quick.AddStretchCell();
		return Row( "Amount", host );
	}

	Widget TextRow( string label, string value, string property, string hint )
	{
		var edit = new LineEdit( this ) { Text = value ?? "", PlaceholderText = hint };

		edit.EditingFinished += () =>
		{
			Edit( () => Set( property, edit.Text ?? "" ) );
			Changed();
		};

		return Row( label, edit );
	}

	static Widget Row( string label, params Widget[] controls )
	{
		var row = new Widget { Layout = Layout.Row() };
		row.Layout.Spacing = 8;

		var name = row.Layout.Add( new Label( label, row ) );
		name.FixedWidth = 80;

		foreach ( var control in controls )
			row.Layout.Add( control, 1 );

		return row;
	}

	// -- Writing and noticing -----------------------------------------------------------------------------------------

	/// <summary>
	/// Make a change the way the editor wants it, so it is recorded for undo and redo.
	/// </summary>
	void Edit( System.Action change )
	{
		if ( authoring is null )
			return;

		SerializedObject.NoteStartEdit( authoring );
		SerializedObject.NotePreChange( authoring );
		change();
		SerializedObject.NoteChanged( authoring );
		SerializedObject.NoteFinishEdit( authoring );
	}

	void Set( string name, object value )
	{
		if ( authoringObject?.GetProperty( name ) is { } property )
			property.SetValue( value );
	}

	/// <summary>
	/// A value changed through this panel: refresh the preview and remember what was written.
	/// </summary>
	void Changed()
	{
		signature = Sign( Values );
		UpdatePreview();
	}

	void UpdatePreview()
	{
		if ( preview is null )
			return;

		var a = Values;
		var made = HandPlacedRules.Build( a.Kind, a.Count, a.Quality, a.Amount, a.Name, a.Description );

		preview.Text = made.Item is { } built ? Describe( built ) : "Nothing yet.";

		var text = made.Error != "" ? $"Needs attention: {made.Error}." : made.Warning != "" ? $"{made.Warning}." : "";
		problems.Text = text;
		problems.Visible = text != "";
	}

	/// <summary>
	/// A change that did not come from this panel (an undo, a redo, another tool): rebuild the fields from the new values.
	/// </summary>
	[EditorEvent.Frame]
	public void Frame()
	{
		if ( !this.IsValid() || body is null || !item.IsValid() || Sign( Values ) == signature )
			return;

		RebuildFields();
	}

	// -- Text and small widgets -----------------------------------------------------------------------------------------

	static readonly Color Dim = Color.Parse( "#8a95a3" ) ?? Color.Gray;
	static readonly Color Bright = Color.Parse( "#e6eef7" ) ?? Color.White;
	static readonly Color Bad = Color.Parse( "#ff6b6b" ) ?? Color.Red;

	static Label Note( string text, Color color, float size = 10 )
	{
		var label = new Label( text ) { WordWrap = true, Color = color };
		return label;
	}

	static string StackText( int count, int max ) => max > 1 ? $"x{count} / {max}" : $"x{count}";

	static string Money( long amount ) => $"${amount:N0}";

	static long ParseMoney( string text, long fallback )
	{
		var digits = new string( (text ?? "").Where( char.IsDigit ).ToArray() );
		return long.TryParse( digits, out var value ) ? value : fallback;
	}

	static string Describe( ItemData made ) => made.Kind == ItemCatalog.Cash
		? CashRules.Format( made.Amount )
		: string.IsNullOrEmpty( made.Kind )
			? made.Name
			: ItemCatalog.QualityMattersOf( made.Kind )
				? $"{made.Name} x{made.Count}, quality {made.Quality}"
				: $"{made.Name} x{made.Count}";

	/// <summary>
	/// A bar that fills as the quality goes up, red to green.
	/// </summary>
	sealed class QualityBar : Widget
	{
		public int Value { get; set; }

		public QualityBar( Widget parent ) : base( parent )
		{
			FixedHeight = 14;
		}

		protected override void OnPaint()
		{
			var rect = LocalRect;

			Paint.ClearPen();
			Paint.SetBrush( Color.Parse( "#1c2128" ) ?? Color.Black );
			Paint.DrawRect( rect, 4 );

			var t = System.Math.Clamp( Value / 100f, 0f, 1f );
			var fill = Color.Lerp( Color.Parse( "#d9534f" ) ?? Color.Red, Color.Parse( "#5fbf6f" ) ?? Color.Green, t );

			Paint.SetBrush( fill );
			Paint.DrawRect( new Rect( rect.Left, rect.Top, rect.Width * t, rect.Height ), 4 );
		}
	}
}
