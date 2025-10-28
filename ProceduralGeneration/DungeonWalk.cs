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
        private int WIDTH = 50;
        private int HEIGHT = 20;
        private Hexagons hex = new Hexagons();
        private dungeon_t dungeon;
        private Random rand = new Random();
        private bool connected = true;
        private HashSet<(int, int)> visited = new HashSet<(int, int)>();

        static void Main(string[] args)
        {
            var procedural = new DungeonWalk();

            procedural.dungeon = procedural.InitDungeon(procedural.WIDTH, procedural.HEIGHT);

            List<path_t> all_paths = procedural.RandomWalk(procedural.dungeon, 5, 25);

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
            // List<(int, int)> pathEnds = new List<(int, int)> { };

            for (int n = 0; n < walks; n++)
            {
                // start from random x, y pos
                // except for the initial path, if later paths do not intersect with existing carved points, connect the current (non-connected) path
                // with random other existing path.
                (int x, int y) = (rand.Next(HEIGHT), rand.Next(WIDTH));
                (int x_link, int y_link) = (x, y);

                // before carving out a new path, always set connected to false.
                
                if (visited.Count != 0) (x_link, y_link) = visited.ElementAt(rand.Next(visited.Count));
                path_t path = Walk(x, y, maxLength, dungeon);
                randomPaths.Add(path);

                foreach ((int x_new, int y_new) in path) visited.Add((x_new, y_new));
                
                // determine whether this is an isolated path and needs to be connected to the main dungeon.
                // if connected is true after Walk, then [path] is connected to the current dungeon. else, use JPS to connect [path] to some random end point.
                if (!connected && visited.Count != 0)
                {
                    path_t lerp = new path_t();
                    // draw linearly-interpolated path from (x, y) to (x_link, y_link)
                    List<Hexagons.HexCoord> hex_line = hex.hex_lerp(new Hexagons.Odd_R(x, y), new Hexagons.Odd_R(x_link, y_link));

                    foreach (Hexagons.HexCoord hex in hex_line)
                    {
                        switch (hex)
                        {
                            case (Hexagons.Cube H):
                                break;

                            case (Hexagons.Axial H):
                                break;

                            case (Hexagons.Odd_R H):
                                dungeon[H.col][H.row] = '@';
                                lerp.Add((H.col, H.row));
                                break;
                        }
                    }

                    randomPaths.Add(lerp);
                }

                dungeon[x][y] = '*';

                connected = false;
            }

            return randomPaths;
        }

        // helper functions are privates
        path_t Walk(int x, int y, int maxLength, dungeon_t dungeon)
        {
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

            var directions = new List<(int x_dir, int y_dir)>()
            {
                // UNITY USES ODD-R OFFSET COORDINATES FOR FLAT-TOP HEX GRID
                (0,-1), (0,1), // left-half-offset column
                (1,1), (1,-1), // right-half-offset column
                (-1,0), (1,0), // direct row
            };

            // pick random direction
            int idx = rand.Next(directions.Count);
            x += directions[idx].x_dir;
            y += directions[idx].y_dir;

            path.AddRange(Walk(x, y, maxLength - 1, dungeon));

            return path;
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