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
    using dungeon_t = List<List<Char>>;
    // we start out with a grid of all walls

    class DungeonWalk
    {
        private int WIDTH = 7;
        private int HEIGHT = 5;
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

            List<path_t> all_paths = procedural.RandomWalk(procedural.dungeon, 1, 20);
            procedural.PrintDungeon();
        }

        dungeon_t InitDungeon(int w, int h)
        {
            dungeon_t dungeon = new dungeon_t(h);
            for (int i = 0; i < h; i++)
            {
                dungeon.Add(new List<Char>(Enumerable.Repeat('|', w)));
            }
            return dungeon;
        }

        List<path_t> RandomWalk(dungeon_t dungeon, int walks, int maxLength)
        {
            List<path_t> randomPaths = new List<path_t> { };

            for (int n = 0; n < walks; n++)
            {
                // start from random x, y pos
                // except for the initial path, if later paths do not intersect with existing carved points, connect the current (non-connected) path
                // with random other existing path.
                (int x, int y) = (rand.Next(HEIGHT), rand.Next(WIDTH));
                (int x_link, int y_link) = (x, y);

                if (visited.Count != 0) (x_link, y_link) = visited.ElementAt(rand.Next(visited.Count));
                List<double> weights = Enumerable.Repeat(1.0 / 6, 6).ToList();
                path_t path = Walk(x, y, maxLength, dungeon, weights);
                randomPaths.Add(path);

                foreach ((int x_new, int y_new) in path) visited.Add((x_new, y_new));
                
                // determine whether this is an isolated path and needs to be connected to the main dungeon.
                // if connected is true after Walk, then [path] is connected to the current dungeon. else, use JPS to connect [path] to some random end point.
                if (!connected && visited.Count != 0)
                {
                    path_t lerp = new path_t();
                    // draw linearly-interpolated path from (x, y) to (x_link, y_link)
                    List<Hexagons.HexCoord> hex_line = hex.hex_lerp(new Hexagons.Odd_Q(x, y), new Hexagons.Odd_Q(x_link, y_link));

                    foreach (Hexagons.HexCoord hex in hex_line)
                    {
                        switch (hex)
                        {
                            case (Hexagons.Odd_Q H):
                                dungeon[H.row][H.col] = '@';
                                lerp.Add((H.row, H.col));
                                break;
                        }
                    }
                    randomPaths.Add(lerp);
                }

                // before carving out a new path, always set connected to false.
                dungeon[x][y] = '*';
                connected = false;
            }
            return randomPaths;
        }

        path_t Walk(int x, int y, int maxLength, dungeon_t dungeon, List<double> weights)
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
            dungeon[x][y] = '~';
            if (maxLength == 0)
            {
                // if we have already carved out (x, y), this means this path is connected to the existing dungeon.
                if (visited.Contains((x, y))) connected = true;

                return new path_t { (x, y) };
            }
            else
            {
                dungeon[x][y] = ' ';
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
            Console.WriteLine("at " + y.ToString() + " " + x.ToString());

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
            weights = RecomputeWeights(directions[idx], weights, 0.5);

            path.AddRange(Walk(x, y, maxLength - 1, dungeon, weights));

            return path;
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
                int dist = hex.hex_len_shortest_path(
                    sampled_dir, dir, blocked
                );
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
            foreach (var row in dungeon)
            {
                Console.WriteLine(string.Join("", row));
            }
        }
    }
}