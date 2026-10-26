namespace Omnipresence.Editor;

/// <summary>
/// The Hardware section of <see cref="NaviComputerInspector"/>: the hardware the computer is built with, which is what it
/// can do beyond its own screen and files (<see cref="NaviHardware"/>, docs in the Navi README). One card per installed
/// piece, saying what it provides and with a button that takes it out, then a picker that installs another, and a line
/// saying what the computer can do now: online or offline, sound or beep only.
/// </summary>
/// <remarks>
/// <para>
/// A component in the scene being edited is the authored setup (<see cref="NaviComputer.Hardware"/>): a new computer
/// starts with it, and once it has a save the save keeps it. A component of the running game (<see cref="Scene.IsEditor"/>
/// off, as selecting the computer in play mode gives) is the computer the player uses, so installing and taking out go
/// through <see cref="NaviComputer.InstallHardware(NaviHardwareResource)"/> and <see cref="NaviComputer.RemoveHardware"/>,
/// which is the path the game itself uses: the apps hear about it and the save keeps it. Those are the host's, so on a
/// client the buttons are off.
/// </para>
/// <para>
/// A change to the scene's list goes through <see cref="SerializedObject"/> (start edit, set, changed, finish edit) so the
/// editor records it for undo and redo, and the panel draws itself again when something else changed the list (an undo,
/// the game starting or stopping).
/// </para>
/// </remarks>
public sealed partial class NaviComputerInspector
{
	/// <summary>
	/// The installed hardware, and the rest of the project's hardware to install.
	/// </summary>
	sealed class HardwarePanel : Widget
	{
		/// <summary>
		/// About how many characters of a note fit on a line, and how tall a line is. A wrapped label doesn't report the
		/// height of its wrapped text, so it is given the lines it needs, as in <see cref="SavedStatePanel"/>.
		/// </summary>
		const int CharsPerLine = 44;
		const int LineHeight = 15;

		static readonly Color Dim = Hex( "#8a95a3" );
		static readonly Color Warn = Hex( "#e8a33d" );

		readonly NaviComputer computer;
		readonly SerializedObject serialized;
		readonly SerializedProperty property;
		string signature = "";

		public HardwarePanel( Widget parent, NaviComputer computer, SerializedObject serialized, SerializedProperty property ) : base( parent )
		{
			this.computer = computer;
			this.serialized = serialized;
			this.property = property;

			Layout = Layout.Column();
			Layout.Spacing = 6;

			Rebuild();
		}

		/// <summary>
		/// The computer being inspected is the one the game runs, not the scene's setup: the game builds its own copy of
		/// the scene, and play mode selects objects from that one (<see cref="Scene.IsEditor"/> off).
		/// </summary>
		bool Live => computer.IsValid() && computer.Scene is { IsEditor: false };

		/// <summary>
		/// Can the hardware be changed now? The scene's list whenever the game isn't running, the running computer's only
		/// by the host, since installing and taking out is (<see cref="NaviComputer.InstallHardware(string)"/>).
		/// </summary>
		bool CanEdit => Live ? computer.IsAuthority : !Game.IsPlaying;

		List<NaviHardwareResource> Installed => computer.InstalledHardware.ToList();

		/// <summary>
		/// What the panel draws from: where the hardware is read from, which hardware the computer has, and which of it is
		/// turned on.
		/// </summary>
		string Sign() => $"{Game.IsPlaying}|{Live}|"
			+ string.Join( ",", Installed.Select( x => $"{x.Id}{(computer.IsHardwareEnabled( x.Id ) ? "" : " (off)")}" ) );

		/// <summary>
		/// A change that did not come from this panel (an undo, a redo, another tool, the game starting or stopping, the
		/// computer taken on its save): draw it again from the values the computer has now.
		/// </summary>
		[EditorEvent.Frame]
		public void Frame()
		{
			if ( this.IsValid() && computer.IsValid() && Sign() != signature )
				Rebuild();
		}

		void Rebuild()
		{
			Layout.Clear( true );
			signature = Sign();

			var installed = Installed;

			Layout.Add( Summary( installed ) );

			if ( installed.Count == 0 )
				Layout.Add( Note( this, "No hardware: this computer is offline and can only beep.", Warn ) );

			foreach ( var hardware in installed )
			{
				var card = hardware;
				Layout.Add( new HardwareCard( this, computer, card, CanEdit, () => Apply( Installed.Where( x => x.Id != card.Id ) ) ) );
			}

			var available = NaviHardware.All.Where( x => x is not null && installed.All( y => y.Id != x.Id ) )
				.OrderBy( x => x.Name ).ToList();

			if ( available.Count > 0 )
				Layout.Add( Picker( available ) );
			else
				Layout.Add( Note( this, NaviHardware.All.Any()
					? "Every piece of hardware in the project is installed."
					: "The project has no hardware assets.", Dim ) );

			Layout.Add( Note( this, Situation(), CanEdit ? Dim : Warn ) );
		}

		/// <summary>
		/// Where the changes go, said plainly, and what stops them.
		/// </summary>
		string Situation()
		{
			if ( Live )
			{
				return CanEdit
					? "The game is running: this changes the computer now, and its save keeps it."
					: "Only the host changes a computer's hardware.";
			}

			return Game.IsPlaying
				? "This is the scene, not the running computer. Select the computer in play mode to change it now."
				: "A new computer starts with this. A save keeps it after that, and game code changes it from then on.";
		}

		/// <summary>
		/// What the hardware the computer has, turned on, lets it do.
		/// </summary>
		Widget Summary( List<NaviHardwareResource> installed )
		{
			var row = new Widget( this ) { Layout = Layout.Row() };
			row.Layout.Spacing = 4;

			var capabilities = installed.Where( x => computer.IsHardwareEnabled( x.Id ) )
				.SelectMany( x => x.Capabilities ?? [] )
				.Where( x => !string.IsNullOrWhiteSpace( x ) )
				.Select( x => x.Trim().ToLowerInvariant() )
				.Distinct()
				.ToList();

			if ( capabilities.Contains( NaviHardware.Network ) )
				row.Layout.Add( new Badge( row, "Online", "#5ec97a", "It has a network card: the Store, Messages and the web work." ) );
			else
				row.Layout.Add( new Badge( row, "Offline", "#e8a33d", "No network card is turned on: the Store, Messages and the web don't work." ) );

			if ( capabilities.Contains( NaviHardware.Audio ) )
				row.Layout.Add( new Badge( row, "Sound", "#4a9eea", "It has a sound card: apps can play sounds." ) );
			else
				row.Layout.Add( new Badge( row, "Beep only", "#8a95a3", "No sound card is turned on: the computer can only beep." ) );

			foreach ( var capability in capabilities.Where( x => x != NaviHardware.Network && x != NaviHardware.Audio ) )
				row.Layout.Add( new Badge( row, capability, "#9ab0c4", $"The installed hardware provides '{capability}'." ) );

			row.Layout.AddStretchCell();
			return row;
		}

		/// <summary>
		/// The hardware the project has that this computer doesn't: choose one, then install it.
		/// </summary>
		Widget Picker( List<NaviHardwareResource> available )
		{
			var column = new Widget( this ) { Layout = Layout.Column() };
			column.Layout.Spacing = 4;

			var caption = new Label( "ADD HARDWARE", column );
			caption.SetStyles( "color: #7a8a99; font-size: 9px; font-weight: 700;" );
			column.Layout.Add( caption );

			var description = Note( column, "", Dim );
			column.Layout.Add( description );

			void Describe( NaviHardwareResource hardware )
			{
				description.Text = string.IsNullOrWhiteSpace( hardware?.Description ) ? hardware?.Id ?? "" : hardware.Description;
				description.MinimumHeight = Lines( description.Text );
				description.Update();
			}

			var combo = new ComboBox( column ) { Enabled = CanEdit };

			for ( var i = 0; i < available.Count; i++ )
			{
				var hardware = available[i];

				combo.AddItem( hardware.Name,
					string.IsNullOrWhiteSpace( hardware.Icon ) ? "memory" : hardware.Icon,
					() => Describe( hardware ),
					string.IsNullOrWhiteSpace( hardware.Description ) ? hardware.Id : hardware.Description,
					i == 0, CanEdit );
			}

			var row = column.Layout.AddRow();
			row.Spacing = 6;
			row.Add( combo, 1 );
			row.Add( new Button( "Install", "add", this )
			{
				Enabled = CanEdit,
				ToolTip = "Install the chosen hardware in this computer.",
				Clicked = () =>
				{
					var index = System.Math.Clamp( combo.CurrentIndex, 0, available.Count - 1 );
					Apply( Installed.Append( available[index] ) );
				},
			} );

			combo.CurrentIndex = 0;
			Describe( available[0] );

			return column;
		}

		/// <summary>
		/// The author installed or removed something. The computer the game runs gets it through the game's own methods,
		/// so the apps hear about it and the save keeps it; the scene's computer gets the authored list, written the way
		/// the editor wants it so the change is recorded for undo and redo.
		/// </summary>
		void Apply( IEnumerable<NaviHardwareResource> hardware )
		{
			if ( !CanEdit )
				return;

			var next = hardware.Where( x => x is not null ).DistinctBy( x => x.Id ).ToList();
			var current = Installed;

			if ( next.Select( x => x.Id ).SequenceEqual( current.Select( x => x.Id ) ) )
				return;

			if ( Live )
			{
				foreach ( var gone in current.Where( x => next.All( y => y.Id != x.Id ) ) )
					computer.RemoveHardware( gone.Id );

				foreach ( var added in next.Where( x => current.All( y => y.Id != x.Id ) ) )
					computer.InstallHardware( added );

				Rebuild();
				return;
			}

			if ( property is null )
				return;

			serialized.NoteStartEdit( property );
			serialized.NotePreChange( property );
			property.SetValue( next );
			serialized.NoteChanged( property );
			serialized.NoteFinishEdit( property );

			Rebuild();
		}

		/// <summary>
		/// The height of a note of this text, in the lines it wraps onto.
		/// </summary>
		static int Lines( string text ) =>
			System.Math.Max( 1, ((text ?? "").Length + CharsPerLine - 1) / CharsPerLine ) * LineHeight;

		/// <summary>
		/// A line of explanation, in the size and color of a note.
		/// </summary>
		static Label Note( Widget parent, string text, Color color ) =>
			new( text, parent ) { WordWrap = true, Color = color, MinimumHeight = Lines( text ) };

		/// <summary>
		/// The color of a capability's tag: the two Navi knows by name, then a grey for anything a game added.
		/// </summary>
		static string CapabilityColor( string capability ) => capability switch
		{
			NaviHardware.Network => "#4a9eea",
			NaviHardware.Audio => "#5ec97a",
			_ => "#9ab0c4",
		};

		/// <summary>
		/// One piece of installed hardware: its icon and name, what it is for, what it provides, and a button that takes it
		/// out. A piece the player turned off says so.
		/// </summary>
		sealed class HardwareCard : Widget
		{
			public HardwareCard( Widget parent, NaviComputer computer, NaviHardwareResource hardware, bool editable,
				System.Action remove ) : base( parent )
			{
				ToolTip = hardware.Id;

				Layout = Layout.Column();
				Layout.Spacing = 2;
				Layout.Margin = new Sandbox.UI.Margin( 8, 6, 8, 8 );

				var head = Layout.AddRow();
				head.Spacing = 6;

				var icon = new HardwareIcon( this, string.IsNullOrWhiteSpace( hardware.Icon ) ? "memory" : hardware.Icon )
				{
					FixedWidth = 24,
					FixedHeight = 24,
				};
				head.Add( icon );

				var name = new Label( hardware.Name, this );
				name.SetStyles( "font-weight: 700; color: #e6eef7;" );
				head.Add( name, 1 );

				head.Add( new Button( "", "delete", this )
				{
					ToolTip = $"Take {hardware.Name} out of the computer.",
					FixedWidth = 26,
					FixedHeight = 24,
					Enabled = editable,
					Clicked = remove,
				} );

				if ( !string.IsNullOrWhiteSpace( hardware.Description ) )
				{
					var description = HardwarePanel.Note( this, hardware.Description, Dim );
					Layout.Add( description );
					description.SetStyles( "margin-left: 30px;" );
				}

				var capabilities = (hardware.Capabilities ?? [])
					.Where( x => !string.IsNullOrWhiteSpace( x ) )
					.Select( x => x.Trim().ToLowerInvariant() )
					.Distinct()
					.ToList();

				var chips = Layout.AddRow();
				chips.Spacing = 4;
				chips.Margin = new Sandbox.UI.Margin( 30, 2, 0, 0 );

				foreach ( var capability in capabilities )
					chips.Add( new Badge( this, capability, CapabilityColor( capability ),
						$"It lets the computer {Describe( capability )}." ) );

				if ( !computer.IsHardwareEnabled( hardware.Id ) )
					chips.Add( new Badge( this, "off", "#e8a33d", "The player turned this off: it provides nothing." ) );
				else if ( capabilities.Count == 0 )
					chips.Add( new Badge( this, "nothing", "#8a95a3", "It provides no capability, so it changes nothing yet." ) );

				chips.AddStretchCell();
			}

			static string Describe( string capability ) => capability switch
			{
				NaviHardware.Network => "reach the network",
				NaviHardware.Audio => "play sounds",
				_ => $"have '{capability}'",
			};

			protected override void OnPaint()
			{
				Paint.ClearPen();
				Paint.SetBrush( Hex( "#242429" ) );
				Paint.DrawRect( LocalRect, 5 );

				Paint.SetPen( Hex( Paint.HasMouseOver ? "#3d4653" : "#33333a" ) );
				Paint.ClearBrush();
				Paint.DrawRect( LocalRect.Shrink( 0.5f ), 5 );
			}
		}

		/// <summary>
		/// A hardware's Material icon in a rounded square, as the assets draw it.
		/// </summary>
		sealed class HardwareIcon : Widget
		{
			readonly string icon;

			public HardwareIcon( Widget parent, string icon ) : base( parent )
			{
				this.icon = icon;
			}

			protected override void OnPaint()
			{
				Paint.ClearPen();
				Paint.SetBrush( Hex( "#2c333c" ) );
				Paint.DrawRect( LocalRect, 5 );

				Paint.SetPen( Hex( "#8fc1ea" ) );
				Paint.ClearBrush();
				Paint.DrawIcon( LocalRect, icon, 16, TextFlag.Center );
			}
		}
	}
}
