using Avalonia.Media;
using System;


namespace Spectrum.Services
{
    /// <summary>
    /// Automatically names a color by finding the closest match in a curated
    /// table of well-known named colors — the same mechanism sites like
    /// Coolors use: no analysis of the color itself, just a nearest-neighbor
    /// lookup against a reference database using a perceptual color distance.
    /// </summary>
    public static class ColorNamingService
    {
        // Standard CSS/X11 named colors. A larger or more evocative table
        // (e.g. importing meodai/color-names or calling api.color.pizza)
        // can be swapped in here without touching the matching logic below.
        private static readonly (string Name, byte R, byte G, byte B)[] Reference =
        {
            ("Alice Blue", 240, 248, 255), ("Antique White", 250, 235, 215), ("Aqua", 0, 255, 255), ("Aquamarine", 127, 255, 212),
            ("Azure", 240, 255, 255), ("Beige", 245, 245, 220), ("Bisque", 255, 228, 196), ("Black", 0, 0, 0),
            ("Blanched Almond", 255, 235, 205), ("Blue", 0, 0, 255),   ("Blue Violet", 138, 43, 226), ("Brown", 165, 42, 42),
            ("Burly Wood", 222, 184, 135), ("Cadet Blue", 95, 158, 160),     ("Chartreuse", 127, 255, 0), ("Chocolate", 210, 105, 30),
            ("Coral", 255, 127, 80), ("Cornflower Blue", 100, 149, 237),            ("Cornsilk", 255, 248, 220), ("Crimson", 220, 20, 60),
            ("Dark Blue", 0, 0, 139), ("Dark Cyan", 0, 139, 139),            ("Dark Goldenrod", 184, 134, 11), ("Dark Gray", 169, 169, 169),
            ("Dark Green", 0, 100, 0), ("Dark Khaki", 189, 183, 107),            ("Dark Magenta", 139, 0, 139), ("Dark Olive Green", 85, 107, 47),
            ("Dark Orange", 255, 140, 0), ("Dark Orchid", 153, 50, 204),            ("Dark Red", 139, 0, 0), ("Dark Salmon", 233, 150, 122),
            ("Dark Sea Green", 143, 188, 143), ("Dark Slate Blue", 72, 61, 139),            ("Dark Slate Gray", 47, 79, 79), ("Dark Turquoise", 0, 206, 209),
            ("Dark Violet", 148, 0, 211), ("Deep Pink", 255, 20, 147),            ("Deep Sky Blue", 0, 191, 255), ("Dim Gray", 105, 105, 105),
            ("Dodger Blue", 30, 144, 255), ("Fire Brick", 178, 34, 34),            ("Floral White", 255, 250, 240), ("Forest Green", 34, 139, 34),
            ("Fuchsia", 255, 0, 255), ("Gainsboro", 220, 220, 220),            ("Ghost White", 248, 248, 255), ("Gold", 255, 215, 0),
            ("Goldenrod", 218, 165, 32), ("Gray", 128, 128, 128),            ("Green", 0, 128, 0), ("Green Yellow", 173, 255, 47),
            ("Honeydew", 240, 255, 240), ("Hot Pink", 255, 105, 180),            ("Indian Red", 205, 92, 92), ("Indigo", 75, 0, 130),
            ("Ivory", 255, 255, 240), ("Khaki", 240, 230, 140),            ("Lavender", 230, 230, 250), ("Lavender Blush", 255, 240, 245),
            ("Lawn Green", 124, 252, 0), ("Lemon Chiffon", 255, 250, 205),            ("Light Blue", 173, 216, 230), ("Light Coral", 240, 128, 128),
            ("Light Cyan", 224, 255, 255), ("Light Goldenrod Yellow", 250, 250, 210),            ("Light Gray", 211, 211, 211), ("Light Green", 144, 238, 144),
            ("Light Pink", 255, 182, 193), ("Light Salmon", 255, 160, 122),            ("Light Sea Green", 32, 178, 170), ("Light Sky Blue", 135, 206, 250),
            ("Light Slate Gray", 119, 136, 153), ("Light Steel Blue", 176, 196, 222),            ("Light Yellow", 255, 255, 224), ("Lime", 0, 255, 0),
            ("Lime Green", 50, 205, 50), ("Linen", 250, 240, 230),            ("Maroon", 128, 0, 0), ("Medium Aquamarine", 102, 205, 170),
            ("Medium Blue", 0, 0, 205), ("Medium Orchid", 186, 85, 211),            ("Medium Purple", 147, 112, 219), ("Medium Sea Green", 60, 179, 113),
            ("Medium Slate Blue", 123, 104, 238), ("Medium Spring Green", 0, 250, 154),            ("Medium Turquoise", 72, 209, 204), ("Medium Violet Red", 199, 21, 133),
            ("Midnight Blue", 25, 25, 112), ("Mint Cream", 245, 255, 250),            ("Misty Rose", 255, 228, 225), ("Moccasin", 255, 228, 181),
            ("Navajo White", 255, 222, 173), ("Navy", 0, 0, 128),            ("Old Lace", 253, 245, 230), ("Olive", 128, 128, 0),
            ("Olive Drab", 107, 142, 35), ("Orange", 255, 165, 0),            ("Orange Red", 255, 69, 0), ("Orchid", 218, 112, 214),           
            ("Pale Goldenrod", 238, 232, 170), ("Pale Green", 152, 251, 152),
            ("Pale Turquoise", 175, 238, 238), ("Pale Violet Red", 219, 112, 147),            ("Papaya Whip", 255, 239, 213), ("Peach Puff", 255, 218, 185),
            ("Peru", 205, 133, 63), ("Pink", 255, 192, 203),            ("Plum", 221, 160, 221), ("Powder Blue", 176, 224, 230),        
            ("Purple", 128, 0, 128), ("Rebecca Purple", 102, 51, 153),            ("Red", 255, 0, 0), ("Rosy Brown", 188, 143, 143),      
            ("Royal Blue", 65, 105, 225), ("Saddle Brown", 139, 69, 19),            ("Salmon", 250, 128, 114), ("Sandy Brown", 244, 164, 96),
            ("Sea Green", 46, 139, 87), ("Sea Shell", 255, 245, 238),            ("Sienna", 160, 82, 45), ("Silver", 192, 192, 192),        
            ("Sky Blue", 135, 206, 235), ("Slate Blue", 106, 90, 205),            ("Slate Gray", 112, 128, 144), ("Snow", 255, 250, 250),   
            ("Spring Green", 0, 255, 127), ("Steel Blue", 70, 130, 180),            ("Tan", 210, 180, 140), ("Teal", 0, 128, 128),         
            ("Thistle", 216, 191, 216), ("Tomato", 255, 99, 71),            ("Turquoise", 64, 224, 208), ("Violet", 238, 130, 238),      
            ("Wheat", 245, 222, 179), ("White", 255, 255, 255),            ("White Smoke", 245, 245, 245), ("Yellow", 255, 255, 0),
            ("Yellow Green", 154, 205, 50), ("Scarlet", 255, 36, 0),    ("Ruby Red", 155, 17, 30),   
            ("Cherry Red", 222, 49, 99),    ("Rose Red", 194, 30, 86),    ("Wine Red", 114, 47, 55),    ("Burgundy", 128, 0, 32),    ("Crimson Red", 153, 0, 0),
            ("Brick Red", 178, 34, 34),    ("Rust Red", 183, 65, 14),    ("Mahogany", 192, 64, 0),    ("Blood Red", 102, 0, 0),    ("Coral Red", 255, 64, 64),     
            ("Tangerine", 242, 133, 0),    ("Burnt Orange", 204, 85, 0),    ("Pumpkin", 255, 117, 24),    ("Amber", 255, 191, 0),
            ("Apricot", 251, 206, 177),    ("Peach", 255, 229, 180),    ("Persimmon", 236, 88, 0),    ("Copper", 184, 115, 51),   
            ("Bronze", 205, 127, 50),    ("Caramel", 193, 154, 107),    ("Honey", 235, 167, 0),    ("Sunset Orange", 253, 94, 83),    
            ("Canary Yellow", 255, 255, 153),    ("Butter Yellow", 255, 253, 208),    ("Banana", 255, 225, 53),    ("Mustard", 255, 219, 88),    
            ("Saffron", 244, 196, 48),    ("Dandelion", 240, 225, 48),    ("Sunshine Yellow", 255, 223, 0),    ("Lemon Yellow", 255, 244, 79),   
            ("Corn Yellow", 251, 236, 93),        ("Emerald", 80, 200, 120),    ("Jade", 0, 168, 107),    ("Mint Green", 152, 255, 152),  
            ("Forest", 34, 139, 34),    ("Pine Green", 1, 121, 111),    ("Moss Green", 138, 154, 91),    ("Olive Green", 107, 142, 35),
            ("Sage", 188, 184, 138),    ("Fern Green", 79, 121, 66),    ("Seafoam", 159, 226, 191),    ("Shamrock", 0, 158, 96),  
            ("Kelly Green", 76, 187, 23),    ("Aqua Marine", 0, 255, 204),    ("Caribbean Blue", 0, 204, 204),    ("Lagoon Blue", 0, 119, 139),   
            ("Teal Blue", 0, 128, 128),    ("Peacock Blue", 0, 128, 128),    ("Seafoam Blue", 102, 205, 170),    ("Electric Cyan", 0, 255, 255),     
            ("Azure Blue", 0, 127, 255),    ("Cobalt Blue", 0, 71, 171),    ("Sapphire Blue", 15, 82, 186),    ("Cerulean", 0, 123, 167),    ("Prussian Blue", 0, 49, 83),
            ("Midnight Blue", 25, 25, 112),    ("Navy Blue", 0, 0, 128),    ("Royal Blue", 65, 105, 225),
            ("Electric Blue", 125, 249, 255),    ("Ice Blue", 173, 216, 230),    ("Steel Blue", 70, 130, 180),
            ("Denim Blue", 21, 96, 189),    ("Amethyst", 153, 102, 204),    ("Lavender Purple", 150, 123, 182),    ("Royal Purple", 120, 81, 169),
            ("Deep Purple", 103, 58, 183),    ("Midnight Purple", 45, 10, 75),    ("Eggplant", 97, 64, 81),
            ("Plum Purple", 142, 69, 133),    ("Grape", 111, 45, 168),    ("Violet Purple", 127, 0, 255),
            ("Orchid Purple", 186, 85, 211),    ("Rose Pink", 255, 102, 204),    ("Blush Pink", 255, 192, 203),
            ("Bubblegum Pink", 255, 105, 180),    ("Flamingo Pink", 252, 142, 172),    ("Fuchsia Pink", 255, 0, 255),   
            ("Magenta Pink", 255, 0, 144),    ("Watermelon", 252, 108, 133),    ("Salmon Pink", 250, 128, 114),    ("Espresso", 75, 54, 33),
            ("Coffee", 111, 78, 55),    ("Chocolate Brown", 123, 63, 0),    ("Mocha", 150, 111, 51),
            ("Walnut", 119, 63, 26),    ("Chestnut", 149, 69, 53),    ("Cinnamon", 210, 105, 30),    ("Clay", 150, 111, 51),
            ("Terracotta", 204, 78, 92),    ("Sand", 194, 178, 128),    ("Desert Sand", 237, 201, 175),
            ("Jet Black", 18, 18, 18),           ("Charcoal", 54, 69, 79),    ("Graphite", 65, 65, 65),    ("Slate", 112, 128, 144),    
            ("Ash Gray", 178, 190, 181),    ("Smoke Gray", 132, 136, 132),    ("Stone Gray", 146, 146, 146),   
            ("Silver Gray", 192, 192, 192),    ("Pearl", 234, 224, 200),   ("Cream", 255, 253, 208),    ("Ivory Cream", 255, 255, 240),
        };

        /// <summary>
        /// Returns the name of the closest reference color to <paramref name="color"/>.
        /// </summary>
        public static string GetClosestName(Color color)
        {
            var bestName = "Color";
            var bestDistance = double.MaxValue;

            foreach (var entry in Reference)
            {
                var distance = WeightedDistance(color.R, color.G, color.B, entry.R, entry.G, entry.B);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestName = entry.Name;
                }
            }

            return bestName;
        }

        // The "redmean" weighted RGB distance — a cheap, well-known approximation
        // of perceptual color difference that skews the weighting toward green,
        // since human vision is more sensitive to it than red or blue. This is
        // the same formula ntc.js (the library behind most "name my color" tools)
        // uses internally.
        private static double WeightedDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            double rMean = (r1 + r2) / 2.0;
            double dr = r1 - r2;
            double dg = g1 - g2;
            double db = b1 - b2;

            return Math.Sqrt(
                (2 + rMean / 256.0) * dr * dr +
                4 * dg * dg +
                (2 + (255 - rMean) / 256.0) * db * db);
        }
    }
}