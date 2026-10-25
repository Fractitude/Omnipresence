using Sandbox.UI;
using static Sandbox.Clothing;

namespace Sandbox;

/// <summary>
/// Broad groups of clothing categories for the filter of the Clothing tab.
/// </summary>
/// <summary>
/// Which clothing the Clothing tab shows, on top of the category filter.
/// </summary>
public enum ClothingScope
{
	All,

	/// <summary>
	/// What the outfit being edited has on.
	/// </summary>
	Wearing,

	/// <summary>
	/// Steam inventory items the player bought, not the free ones.
	/// </summary>
	Purchased,

	/// <summary>
	/// Community clothing packages, searched on sbox.game and downloaded when picked.
	/// </summary>
	Workshop
}

public enum ClothingGroup
{
	Hats,
	Hair,
	Face,
	Tops,
	Bottoms,
	Footwear,
	Gloves,
	Accessories,
	Fullbody,
	Underwear,
	Other
}

public static class ClothingGroups
{
	public static string GetIcon( this ClothingGroup group )
	{
		return group switch
		{
			ClothingGroup.Hats => "🎩",
			ClothingGroup.Hair => "💇",
			ClothingGroup.Face => "😐",
			ClothingGroup.Fullbody => "👗",
			ClothingGroup.Tops => "👕",
			ClothingGroup.Bottoms => "👖",
			ClothingGroup.Footwear => "🥾",
			ClothingGroup.Gloves => "🧤",
			ClothingGroup.Accessories => "⌚",
			ClothingGroup.Underwear => "🩲",
			_ => "📦"
		};
	}

	public static ClothingGroup GetGroup( this Clothing clothing )
	{
		return clothing.Category switch
		{
			ClothingCategory.Hat or ClothingCategory.HatBeanie or ClothingCategory.HatFormal or ClothingCategory.HatCostume
				or ClothingCategory.HatUniform or ClothingCategory.HatSpecial or ClothingCategory.Headwear or ClothingCategory.HeadTech
				or ClothingCategory.HeadBand or ClothingCategory.HeadJewel or ClothingCategory.HeadSpecial => ClothingGroup.Hats,

			ClothingCategory.Hair or ClothingCategory.HairShort or ClothingCategory.HairMedium or ClothingCategory.HairLong
				or ClothingCategory.HairUpdo or ClothingCategory.HairSpecial => ClothingGroup.Hair,

			ClothingCategory.Facial or ClothingCategory.Eyes or ClothingCategory.Eyebrows or ClothingCategory.Eyelashes
				or ClothingCategory.MakeupLips or ClothingCategory.MakeupEyeshadow or ClothingCategory.MakeupEyeliner
				or ClothingCategory.MakeupHighlighter or ClothingCategory.MakeupBlush or ClothingCategory.MakeupSpecial
				or ClothingCategory.ComplexionFreckles or ClothingCategory.ComplexionScars or ClothingCategory.ComplexionAcne
				or ClothingCategory.FacialHairMustache or ClothingCategory.FacialHairBeard or ClothingCategory.FacialHairStubble
				or ClothingCategory.FacialHairSideburns or ClothingCategory.FacialHairGoatee => ClothingGroup.Face,

			ClothingCategory.Tops or ClothingCategory.TShirt or ClothingCategory.Sweatshirt or ClothingCategory.Hoodie
				or ClothingCategory.Shirt or ClothingCategory.Vest or ClothingCategory.Knitwear or ClothingCategory.Jacket
				or ClothingCategory.Cardigan or ClothingCategory.Coat or ClothingCategory.Gilet => ClothingGroup.Tops,

			ClothingCategory.Bottoms or ClothingCategory.Shorts or ClothingCategory.Trousers or ClothingCategory.Jeans
				or ClothingCategory.Skirt => ClothingGroup.Bottoms,

			ClothingCategory.Footwear or ClothingCategory.Socks or ClothingCategory.Heels or ClothingCategory.Sandals
				or ClothingCategory.Shoes or ClothingCategory.Trainers or ClothingCategory.Boots or ClothingCategory.Slippers => ClothingGroup.Footwear,

			ClothingCategory.Gloves => ClothingGroup.Gloves,

			ClothingCategory.Eyewear or ClothingCategory.GlassesEye or ClothingCategory.GlassesSun or ClothingCategory.GlassesSpecial
				or ClothingCategory.NecklaceChain or ClothingCategory.NecklacePendant or ClothingCategory.NecklaceSpecial
				or ClothingCategory.EarringStud or ClothingCategory.EarringDangle or ClothingCategory.EarringSpecial
				or ClothingCategory.Wristwear or ClothingCategory.WristWatch or ClothingCategory.WristBand or ClothingCategory.WristJewel
				or ClothingCategory.WristSpecial or ClothingCategory.Ring or ClothingCategory.Piercing or ClothingCategory.PierceNose
				or ClothingCategory.PierceEyebrow or ClothingCategory.PierceSpecial => ClothingGroup.Accessories,

			ClothingCategory.Fullbody or ClothingCategory.Dress or ClothingCategory.Suit or ClothingCategory.Costume
				or ClothingCategory.Uniform => ClothingGroup.Fullbody,

			ClothingCategory.Underwear or ClothingCategory.Bra or ClothingCategory.Underpants => ClothingGroup.Underwear,

			_ => ClothingGroup.Other
		};
	}

	static readonly string[] CategoryPrefixes = ["FacialHair", "Complexion", "Necklace", "Earring", "Glasses", "Makeup", "Pierce", "Wrist", "Hair", "Head", "Hat"];

	/// <summary>
	/// The category name without the prefix its group already says, "HatBeanie" is "Beanie".
	/// </summary>
	public static string GetTitle( this ClothingCategory category )
	{
		var name = category.ToString();

		foreach ( var prefix in CategoryPrefixes )
		{
			// "Special" alone is ambiguous, HatSpecial and HeadSpecial are both hats
			if ( name.Length > prefix.Length && name.StartsWith( prefix ) && !name.EndsWith( "Special" ) )
				return name[prefix.Length..].ToTitleCase();
		}

		return name.ToTitleCase();
	}

	/// <summary>
	/// Female, Male or Other, from the tags of a human skin.
	/// </summary>
	public static string GetSkinGender( this Clothing clothing )
	{
		var tags = clothing.HumanSkinTags;
		if ( tags is null ) return "Other";
		if ( tags.Has( "female" ) ) return "Female";
		if ( tags.Has( "male" ) ) return "Male";
		return "Other";
	}
}
