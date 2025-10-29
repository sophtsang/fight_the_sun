using System;

namespace ProceduralGeneration;

public class Hexagons
{
    public interface HexCoord { }
    public record struct Cube(int q, int r, int s) : HexCoord;
    public record struct Axial(int q, int r) : HexCoord;
    public record struct Odd_Q(int row, int col) : HexCoord;
    // in Unity, we have (row, col) instead of (col, row)
    
    // A class for all functions dealing with 2D hexagonal grids.
    public List<HexCoord> hex_lerp(HexCoord a_, HexCoord b_)
    {
        // linear interpolation to draw a line connecting start and end
        // in flat-top hex grid. we want to work in cube distance.

        // first cast all coordinate representations to Cube.
        (Cube a, Cube b) = (new Cube(), new Cube());
        switch ((a_, b_))
        {
            case (Cube A, Cube B):
                (a, b) = (A, B);
                break;

            case (Axial A, Axial B):
                (a, b) = (new Cube(A.q, A.r, -A.q - A.r), new Cube(B.q, B.r, -B.q - B.r));
                break;

            case (Odd_Q A, Odd_Q B):
                (a, b) = (oddq_to_cube(A), oddq_to_cube(B));
                break;

            default:
                throw new ArgumentException("a and b must have the same coordinate representation.");
        }

        int N = cube_distance(a, b);
        float lerp(int a, int b, float t) => a + (b - a) * t;
        List<HexCoord> path = new List<HexCoord>(N+1);

        for (int n = 0; n <= N; n++)
        {
            // we have ray a -> b, just like in ray-casting and NeRF, we sample
            // N + 1 points along the ray, evenly spaced at each t-th sample: 
            // ray is defined with origin [a], direction [b - a] -> a + (b - a) * t;

            // sampling returns a floating point, need to round to int coordinate
            (float, float, float) nth_frac_sample = (
                lerp(a.q, b.q, (float) 1.0/N * n),
                lerp(a.r, b.r, (float) 1.0/N * n),
                lerp(a.s, b.s, (float) 1.0/N * n)
            );

            Cube nth_sample = cube_round(nth_frac_sample);

            // convert back to original coordinate representation.
            switch ((a_, b_))
            {
                case (Cube A, Cube B):
                    path.Add(nth_sample);
                    break;

                case (Axial A, Axial B):
                    path.Add(new Axial(nth_sample.q, nth_sample.r));
                    break;

                case (Odd_Q A, Odd_Q B):
                    path.Add(cube_to_oddq(nth_sample));
                    break;
            }
        }

        return path;
    }

    public Cube cube_round((float q, float r, float s) frac)
    {
        (double q, double r, double s) = (
            Math.Round(frac.q),
            Math.Round(frac.r),
            Math.Round(frac.s)
        );

        (double dq, double dr, double ds) = (
            Math.Abs(q - frac.q),
            Math.Abs(r - frac.r),
            Math.Abs(s - frac.s)
        );

        if (dq > dr && dq > ds) q = -r - s;
        else if (dr > ds) r = -q - s;
        else s = -q - r;

        return new Cube((int)q, (int)r, (int)s);
    }

    public int cube_distance(Cube a, Cube b)
    {
        // element-wise subtraction, then half the Manhattan distance of 3d 
        // cube -> because a hexagon is just the diagonal of a cube.
        return (Math.Abs(a.q - b.q) + Math.Abs(a.r- b.r) + Math.Abs(a.s - b.s)) / 2;
    }

    public Cube oddq_to_cube(Odd_Q hex)
    {
        int parity = hex.col & 1;
        int q = hex.col;
        int r = hex.row - (hex.col - parity) / 2;
        return new Cube(q, r, -q-r);
    }

    public Odd_Q cube_to_oddq(Cube hex)
    {
        int parity = hex.q & 1;
        int col = hex.q;
        int row = hex.r + (hex.q - parity) / 2;
        return new Odd_Q(row, col);
    }

    public Cube cube_add(Cube hex, Cube vec)
    {
        return new Cube(hex.q + vec.q, hex.r + vec.r, hex.s + vec.s);
    }

    public Cube cube_scale(Cube hex, int factor)
    {
        return new Cube(hex.q * factor, hex.r * factor, hex.s * factor);
    }

    public List<Cube> cube_ring(Cube center, int radius)
    {
        List<Cube> direction_vectors = new List<Cube>()
        {
            new Cube(1, 0, -1), new Cube(1, -1, 0), new Cube(0, -1, 1),
            new Cube(-1, 0, 1), new Cube(-1, 1, 0), new Cube(0, 1, -1)
        };

        // (1, 1, -2) -> (1, 2, -3) -> (2, 1, -3) -> (2, 0, -2) -> (1, 0, -1) -> (0, 1, -1) -> (0, 2, -2)
        List<Cube> ring = new List<Cube>();
        Cube hex = cube_add(center, cube_scale(direction_vectors[4], radius));

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < radius; j++)
            {
                ring.Add(hex);
                hex = cube_add(hex, direction_vectors[i]);
            }
        }
        return ring;
    }
    
    public int hex_len_shortest_path(Cube a, Cube b, List<Cube> blocked)
    {
        List<Cube> direction_vectors = new List<Cube>()
        {
            new Cube(1, 0, -1), new Cube(1, -1, 0), new Cube(0, -1, 1),
            new Cube(-1, 0, 1), new Cube(-1, 1, 0), new Cube(0, 1, -1)
        };

        HashSet<Cube> visited = new HashSet<Cube>();
        Queue<(Cube, int)> unexplored = new Queue<(Cube, int)>();
        unexplored.Enqueue((a, 0));

        while (unexplored.Count != 0)
        {
            (Cube curr, int layer) = unexplored.Dequeue();
            if (curr == b)
            {
                return layer;
            }

            foreach (Cube vec in direction_vectors)
            {
                Cube next = cube_add(curr, vec);
                // next has not been visited before and is not an obstacle
                if (!visited.Contains(next) && !blocked.Contains(next))
                {
                    unexplored.Enqueue((next, layer + 1));
                }
            }
        }

        return -1;
    }
}
