// See https://aka.ms/new-console-template for more information
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using ProceduralGeneration;

// Program Dungeon Generation with Naive Random Walk
// Later Implement Wave Collapse Function and Entropy

namespace NaiveRandomWalk
{
    using path_t = List<(int, int)>;
    using dungeon_t = List<List<Tile>>;
    enum Tile
    {
        WALL,
        PAVEMENT,
        PAVEMENT_START,
        PAVEMENT_END,
        GRASS
    };

    enum MapType
    {
        ALLEY,
        OPEN
    }

    // we start out with a grid of all walls

    class DungeonWalk
    {
        private int WIDTH = 64;
        private int HEIGHT = 20;
        private Hexagons hex = new Hexagons();
        private dungeon_t dungeon;
        private Random rand = new Random();
        private bool connected = true;
        private HashSet<(int, int)> visited = new HashSet<(int, int)>();
        private List<Hexagons.Cube> blocked;

        static void Main(string[] args)
        {
            var procedural = new DungeonWalk();

            procedural.dungeon = procedural.InitDungeon(procedural.WIDTH, procedural.HEIGHT);

            // for recomputing weights
            procedural.blocked = procedural.hex.cube_ring(new Hexagons.Cube(0, 0, 0), 2);
            procedural.blocked.Add(new Hexagons.Cube(0, 0, 0));

            List<path_t> all_paths = procedural.RandomWalk(procedural.dungeon, 20, 10, MapType.ALLEY);
            procedural.CellularAutomata(procedural.dungeon);
            procedural.PrintDungeon();
        }

        dungeon_t InitDungeon(int w, int h)
        {
            dungeon_t dungeon = new dungeon_t(h);
            for (int i = 0; i < h; i++)
            {
                dungeon.Add(new List<Tile>(Enumerable.Repeat(Tile.WALL, w)));
            }
            return dungeon;
        }

        List<path_t> RandomWalk(dungeon_t dungeon, int walks, int maxLength, MapType mapType)
        {
            // for mapType ALLEY, initalize a slightly diagonal blank that represents the main alleyway.
            double alpha = 0.0;
            List<path_t> randomPaths = new List<path_t>
            {
                LerpBlank(
                    new Hexagons.Odd_Q(rand.Next(HEIGHT / 2 - 6, HEIGHT / 2 - 2), rand.Next(1, 3)),
                    new Hexagons.Odd_Q(rand.Next(HEIGHT / 2 + 2, HEIGHT / 2 + 6), rand.Next(WIDTH - 3, WIDTH - 1))
                )
            };

            for (int n = 0; n < walks; n++)
            {
                // start from random x, y pos
                // except for the initial path, if later paths do not intersect with existing carved points, connect the current (non-connected) path
                // with random other existing path.
                (int x, int y) = (rand.Next(HEIGHT), rand.Next(WIDTH));

                // different mapTypes have different constraints on map shape -> all paths should be connected to main alleyway
                switch (mapType)
                {
                    case MapType.ALLEY:
                        if (randomPaths.Count != 0)
                        {
                            path_t rand_path = randomPaths[rand.Next(randomPaths.Count)];
                            (x, y) = rand_path[rand.Next(rand_path.Count)];
                            alpha = 0.5;
                        }
                        break;
                    case MapType.OPEN:
                        // open fields should be completely randomized, alpha = 0.0
                        break;
                }
                
                (int x_link, int y_link) = (x, y);

                if (visited.Count != 0) (x_link, y_link) = visited.ElementAt(rand.Next(visited.Count));
                List<double> weights = Enumerable.Repeat(1.0 / 6, 6).ToList();
                path_t path = Walk(x, y, maxLength, dungeon, weights, alpha);
                randomPaths.Add(path);

                foreach ((int x_new, int y_new) in path) visited.Add((x_new, y_new));

                // determine whether this is an isolated path and needs to be connected to the main dungeon.
                // if connected is true after Walk, then [path] is connected to the current dungeon. else, use JPS to connect [path] to some random end point.
                if (!connected && visited.Count != 0)
                {
                    randomPaths.Add(LerpBlank(new Hexagons.Odd_Q(x, y), new Hexagons.Odd_Q(x_link, y_link)));
                }

                // before carving out a new path, always set connected to false.
                dungeon[x][y] = Tile.PAVEMENT_START;
                connected = false;
            }
            return randomPaths;
        }
        path_t Walk(int x, int y, int maxLength, dungeon_t dungeon, List<double> weights, double alpha)
        {
            // UNITY USES ODD-Q OFFSET COORDINATES FOR FLAT-TOP HEX GRID
            // except flipped along horizontal
            // check notes for clarification
            // formatted as (r, q): (row, col)
            // out of bounds, stop walking.
            if (x < 0 || x >= HEIGHT || y < 0 || y >= WIDTH)
            {
                // in this case: backtrack to allow the path to grow to maxLength.
                return new path_t();
            }
            // at maxLength, stop walking, but return valid stopping x, y.
            dungeon[x][y] = Tile.PAVEMENT_END;
            if (maxLength == 0)
            {
                // if we have already carved out (x, y), this means this path is connected to the existing dungeon.
                if (visited.Contains((x, y))) connected = true;

                return new path_t { (x, y) };
            }
            else
            {
                dungeon[x][y] = Tile.PAVEMENT;
                if (visited.Contains((x, y))) connected = true;
            }

            // Console.WriteLine("tilemap.SetTile(new Vector3Int(" + (-x + 5).ToString() + ", " + (y - 10).ToString() + ", 0), tileToPlace);");

            path_t path = new path_t { (x, y) };

            List<Hexagons.Cube> directions = new List<Hexagons.Cube>()
            {
                new Hexagons.Cube(1, 0, -1), new Hexagons.Cube(1, -1, 0), new Hexagons.Cube(0, -1, 1),
                new Hexagons.Cube(-1, 0, 1), new Hexagons.Cube(-1, 1, 0), new Hexagons.Cube(0, 1, -1)
            };

            // sample next direction in directions given weights.
            int idx = InverseTransformSampling(weights);
            Hexagons.Cube cube = hex.oddq_to_cube(new Hexagons.Odd_Q(x, y));
            Hexagons.Odd_Q odd_q = hex.cube_to_oddq(hex.cube_add(cube, directions[idx]));
            (x, y) = (odd_q.row, odd_q.col);

            // if we are at a boundary, make the weights of going in this direction very low, then try resampling again.
            if (x < 0 || x >= HEIGHT || y < 0 || y >= WIDTH)
            {
                weights = RecomputeWeights(directions[idx], weights, -10.0);
                idx = InverseTransformSampling(weights);
                odd_q = hex.cube_to_oddq(hex.cube_add(cube, directions[idx]));
                (x, y) = (odd_q.row, odd_q.col);
            }
            // re-compute weights after sampling:
            weights = RecomputeWeights(directions[idx], weights, alpha);

            path.AddRange(Walk(x, y, maxLength - 1, dungeon, weights, alpha));

            return path;
        }

        path_t LerpBlank(Hexagons.Odd_Q a, Hexagons.Odd_Q b)
        {
            path_t lerp = new path_t();
            // draw linearly-interpolated path from (x, y) to (x_link, y_link)
            List<Hexagons.HexCoord> hex_line = hex.hex_lerp(a, b);

            foreach (Hexagons.HexCoord hex in hex_line)
            {
                switch (hex)
                {
                    case (Hexagons.Odd_Q H):
                        dungeon[H.row][H.col] = Tile.GRASS;
                        lerp.Add((H.row, H.col));
                        break;
                }
            }
            return lerp;
        }

        void CellularAutomata(dungeon_t dungeon)
        {
            // 4-5 rule: eventually want 45% of the dungeon to be open, non-walls
            // if a tile is a wall: >= 3/6 of its neighbors are walls -> remain a wall
            // if a tile is not a wall: >= 4/6 of its neighbors are walls -> become a wall
            List<Hexagons.Cube> directions = new List<Hexagons.Cube>()
            {
                new Hexagons.Cube(1, 0, -1), new Hexagons.Cube(1, -1, 0), new Hexagons.Cube(0, -1, 1),
                new Hexagons.Cube(-1, 0, 1), new Hexagons.Cube(-1, 1, 0), new Hexagons.Cube(0, 1, -1)
            };

            for (int row = 0; row < HEIGHT; row++)
            {
                for (int col = 0; col < WIDTH; col++)
                {
                    int walls = 0;
                    Hexagons.Cube tile = hex.oddq_to_cube(new Hexagons.Odd_Q(row, col));
                    foreach (Hexagons.Cube dir in directions)
                    {
                        Hexagons.Odd_Q neighbor = hex.cube_to_oddq(hex.cube_add(tile, dir));

                        if (neighbor.row < 0 || neighbor.row >= HEIGHT || neighbor.col < 0 || neighbor.col >= WIDTH) walls += 1;
                        else walls += (dungeon[neighbor.row][neighbor.col] == Tile.WALL) ? 1 : 0;
                    }

                    if (dungeon[row][col] == Tile.WALL && walls <= 3)
                    {
                        dungeon[row][col] = Tile.PAVEMENT;
                    } else if (dungeon[row][col] != Tile.WALL && walls > 4)
                    {
                        dungeon[row][col] = Tile.WALL;
                    }
                }
            }
        }

        List<double> RecomputeWeights(Hexagons.Cube sampled_dir, List<double> weights, double alpha)
        {
            // function for recomputing weights given sampled direction and alpha
            List<Hexagons.Cube> directions = new List<Hexagons.Cube>()
            {
                new Hexagons.Cube(1, 0, -1), new Hexagons.Cube(1, -1, 0), new Hexagons.Cube(0, -1, 1),
                new Hexagons.Cube(-1, 0, 1), new Hexagons.Cube(-1, 1, 0), new Hexagons.Cube(0, 1, -1)
            };

            foreach ((int i, Hexagons.Cube dir) in directions.Select((dir, idx) => (idx, dir)))
            {
                int dist = Math.Max(1, hex.hex_len_shortest_path(
                    sampled_dir, dir, blocked
                ));
                weights[i] *= Math.Exp(-alpha * dist);
            }
            return weights.Select(w => w / weights.Sum()).ToList();
        }
        
        int InverseTransformSampling(List<double> weights)
        {
            // a little like prefix sum: the ith bin's range is (prefix sum of bin[:i], prefix sum of bin[:i] + weight[i]) 
            // i.e. weights = [0.3, 0.5, 0.2]
            // bins: [   |     |  ] -> ranges: [0, 0.3), [0.3, 0.8), [0.8, 1), ranges are [inclusive, exclusive)
            //       0  0.3   0.8 1
            // sample a number from Unif(0, 1) and returns the index of the bin that sampled number falls into.
            double idx = rand.NextDouble();
            double prefix_weights = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (idx >= prefix_weights && idx < prefix_weights + weights[i])
                {
                    return i;
                }
                prefix_weights += weights[i];
            }
            return -1;
        }

        void PrintDungeon()
        {
            foreach (List<Tile> row in dungeon)
            {
                string line = "";
                foreach (Tile tile in row)
                {
                    switch (tile)
                    {
                        case Tile.WALL:
                            line += "#";
                            break;
                        case Tile.GRASS:
                            line += ".";
                            break;
                        case Tile.PAVEMENT:
                            line += ".";
                            break;
                        case Tile.PAVEMENT_START:
                            line += ".";
                            break;
                        case Tile.PAVEMENT_END:
                            line += ".";
                            break;
                    }
                }
                Console.WriteLine(line);
            }
        }
    }
}