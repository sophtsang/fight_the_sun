using System.Runtime;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using ProceduralGeneration;
using System.Runtime.InteropServices;

namespace NaiveRandomWalk.WaveFunctionCollapse
{
    // adjacency rules for what tiles can appear next to other tiles
    // map direction to tile type
    using adjacency_t = List<(List<List<Tile>>, List<List<Tile>>, (int, int))>;
    // map the frequency that tile_a should appear relative to tile_b
    // ?
    using frequency_t = Dictionary<int, int>;
    using path_t = List<(int, int)>;
    using dungeon_t = List<List<Tile>>;
    
    public class WFS : DungeonWalk
    {
        private adjacency_t adjacencyRules;
        private frequency_t frequencyHints;
        private dungeon_t dungeon;

        static void Main(string[] args)
        {
            var procedural = new WFS();
            procedural.frequencyHints = new frequency_t();
            procedural.adjacencyRules = new adjacency_t();
            procedural.dungeon = new dungeon_t()
            {
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.WALL, Tile.WALL, Tile.WALL, Tile.WALL },
                new List<Tile> { Tile.PAVEMENT, Tile.WALL, Tile.PAVEMENT, Tile.PAVEMENT },
            };
            // procedural.PopulateFrequencyHints(procedural.dungeon, 3);

            dungeon_t a = new dungeon_t()
            {
                new List<Tile> { Tile.PAVEMENT, Tile.PAVEMENT, Tile.PAVEMENT },
                new List<Tile> { Tile.WALL, Tile.WALL, Tile.GRASS },
                new List<Tile> { Tile.PAVEMENT, Tile.PAVEMENT, Tile.PAVEMENT }
            };

            dungeon_t b = new dungeon_t()
            {
                new List<Tile> { Tile.PAVEMENT, Tile.PAVEMENT, Tile.GRASS },
                new List<Tile> { Tile.WALL, Tile.GRASS, Tile.PAVEMENT },
                new List<Tile> { Tile.PAVEMENT, Tile.PAVEMENT, Tile.GRASS }
            };
            Console.WriteLine(procedural.Compatible(a, b, (-1, 0)));
        }

        void PreprocessImage(dungeon_t image, int tileSize)
        {
            // populates frequencyHints and adjacencyRules
            // returns mapping of tiles and tile indices.
            List<(dungeon_t, int)> all_tile_squares = PopulateFrequencyHints(image, tileSize);
            Dictionary<int, dungeon_t> idx_to_tile = PopulateAdjacencyRules(all_tile_squares);
        }

        List<List<int>> WFCCore()
        {
            List<List<int>> grid = new List<List<int>>();

            // recursive backtracking algorithm that:
            // 1) chooses a cell at random, consider only the cells with the fewest possible tile_idx
            // 2) choose random valid (allowed by adjacency rules) tile_idx to lock in based on 
            //    weighted sampling from frequency hints
            // 3) propogate effects of locking in, remove locked-in value from possibilities in 3x3 
            //    adjacent neighbors
            // 4) if removed final possibility of cell, and cannot choose another -> backtrack
            

            return grid;
        }

        void PostprocessImage(List<List<int>> grid, Dictionary<int, dungeon_t> idx_to_tile)
        {
            // convert grid of tile idx to tile.

        }

        List<(dungeon_t, int)> PopulateFrequencyHints(dungeon_t image, int tileSize)
        {
            // generate adjcency rules and frequency hints for core WFC algorithm
            // enumerate all the tile-sized squares of pixels from input image
            // enumerate for all top-left pixel in the original image
            // to allow for easy wrapping around -> add tileSize - 1 columns from the left
            // to the right
            // add tileSize - 1 rows from the top to the bottom
            // i.e. [.#..]                    [.#..|.#]
            //      [.#..] => tileSize = 3 => [.#..|.#]
            //      [####]                    [####|##]
            //      [.#..]                    [.#..|.#]
            //                                ---------
            //                                [.#..|.#]
            //                                [.#..|.#]
            // for hexagonal grids -> do i need hexagonal sliding windows
            // instead of tileSize -> radius of hexagonal ring: figure this out later
            dungeon_t padded_image = image
                .Select(row => new List<Tile>(row))
                .ToList();
            for (int i = 0; i < image.Count; i++)
            {
                padded_image[i].AddRange(padded_image[i][..(tileSize - 1)]);
            }
            padded_image.AddRange(padded_image[..(tileSize - 1)]);
            // specific tile square : tile index
            List<(dungeon_t tile, int tile_idx)> all_tile_squares = new List<(dungeon_t, int)>();
            for (int i = 0; i < image.Count; i++)
            {
                for (int j = 0; j < image[i].Count; j++)
                {
                    // tileSize x tileSize window with i, j as top left corner
                    dungeon_t tile_square = padded_image
                        .Skip(i).Take(tileSize)
                        .Select(row => row.Skip(j).Take(tileSize).ToList())
                        .ToList();
                    // if exist_idx != -1, then exist_idx is the tile index of tile tile_square
                    int exist_idx = all_tile_squares.FindIndex(sq => tileEquals(sq.tile, tile_square));
                    PrintDungeon(tile_square);
                    // Console.WriteLine(exist_idx.ToString());
                    if (exist_idx == -1)
                    {
                        // tile_index : frequency
                        frequencyHints.Add(i * image[i].Count + j, 1);
                        all_tile_squares.Add((tile_square, i * image[i].Count + j));
                    }
                    else frequencyHints[exist_idx] += 1;
                }
            }
            return all_tile_squares;
        }

        Dictionary<int, dungeon_t> PopulateAdjacencyRules(List<(dungeon_t, int)> all_tile_squares)
        {
            // when assigning a tile index to each cell in the grid, only the Tile value of the top left pixel is used
            // need to make sure that when a tile is placed within tileSize pixels
            // of an already-placed tile's top-left pixel -> new tile's pixels don't conflict w/ pixels of existing.
            // when we place a new top-left cell down -> we "color", but not populate, the other cells in the tileSize
            // square w/ corresponding colors of the tile_square
            // we can only populate the "colored" cells w/ the same color.
            // we take all_tile_squares tiles and idx from PopulateFrequencyHints
            // also generates mapping of tile index to dungeon_t.
            List<(int, int)> directions = new List<(int, int)>
            {
                (0, 1), (0, -1), (1, 0), (-1, 0)
            };

            Dictionary<int, dungeon_t> idx_to_tile = new Dictionary<int, dungeon_t>();
            foreach ((dungeon_t tile_a, int idx_a) in all_tile_squares)
            {
                if (!idx_to_tile.ContainsKey(idx_a)) idx_to_tile[idx_a] = tile_a;
                foreach ((dungeon_t tile_b, int idx_b) in all_tile_squares)
                {
                    // change this into hexagonal neighbor directions later:
                    foreach ((int, int) dir in directions)
                    {
                        // TODO: fix implementation of adjacencyRules data structure later.
                        if (Compatible(tile_a, tile_b, dir)) adjacencyRules.Add((tile_a, tile_b, dir));
                    }
                }
            }

            return idx_to_tile;
        }
        
        bool Compatible(dungeon_t a, dungeon_t b, (int dx, int dy) direction)
        {
            // lets do this for square grid first, will be super hard for hexagons i might die.
            // if a = ... , b = ..@, direction = (0, 1) move right
            //        ##@       #@.
            //        ...       ..@
            // only place a if a overlaps b when b is offset in direction (dx, dy)
            // shift b right by 1, down by 0:
            // b_offset = ..
            //            #@
            //            ..
            dungeon_t a_offset = a
                .Skip(Math.Max(direction.dx, 0)).Take(b.Count - Math.Abs(direction.dx))
                .Select(row => row.Skip(Math.Max(direction.dy, 0)).Take(b.Count - Math.Abs(direction.dy)).ToList())
                .ToList();

            dungeon_t b_offset = b
                .Skip(Math.Max(-direction.dx, 0)).Take(b.Count - Math.Abs(direction.dx))
                .Select(row => row.Skip(Math.Max(-direction.dy, 0)).Take(b.Count - Math.Abs(direction.dy)).ToList())
                .ToList();

            return tileEquals(a_offset, b_offset);
        }

        // later just make a dungeon_t class with comparisons, prints...
        bool tileEquals(dungeon_t a, dungeon_t b)
        {
            // compares whether the string representation of the flattened dungeons are the same
            string a_string = TileToString(a.SelectMany(row => row).ToList());
            string b_string = TileToString(b.SelectMany(row => row).ToList());
            return a_string.Equals(b_string);
        }

        void RotateAndReflect(dungeon_t tile_square)
        {
            // do this with hexagonal rotation and reflection
        }

        void WaveFunctionCollapse()
        {
            // each hex grid loation holds an array of booleans for what tiles can and cannot be
            // one tile is selected, given a single random solution from remaining possibilities
            // choice is propogated throughout the grid -> eliminate adjacent possibilities that 
            // don't match the input model
            // backtrack if observation & propogation result in unsolvable contradiction -> choose
            // different possibility
        }

        string TileToString(List<Tile> dungeon_row)
        {
            string line = "";
            foreach (Tile tile in dungeon_row)
            {
                switch (tile)
                {
                    case Tile.WALL:
                        line += "#";
                        break;
                    case Tile.GRASS:
                        line += "@";
                        break;
                    case Tile.PAVEMENT:
                        line += ".";
                        break;
                    case Tile.PAVEMENT_START:
                        line += "*";
                        break;
                    case Tile.PAVEMENT_END:
                        line += "~";
                        break;
                }
            }
            return line;
        }
        
        void PrintDungeon(dungeon_t dungeon)
        {
            foreach (List<Tile> row in dungeon)
            {
                Console.WriteLine(TileToString(row));
            }
        }
    }
}