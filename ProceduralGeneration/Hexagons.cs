using System;

namespace ProceduralGeneration;

public class Hexagons
{
    public interface HexCoord { }
    public record struct Cube(int q, int r, int s) : HexCoord;
    public record struct Axial(int q, int r) : HexCoord;
    public record struct Odd_R(int col, int row) : HexCoord;
    
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

            case (Odd_R A, Odd_R B):
                (a, b) = (oddr_to_cube(A), oddr_to_cube(B));
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

                case (Odd_R A, Odd_R B):
                    path.Add(cube_to_oddr(nth_sample));
                    break;
            }
        }

        return path;
    }

    Cube cube_round((float q, float r, float s) frac)
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

    int cube_distance(Cube a, Cube b)
    {
        // element-wise subtraction, then half the Manhattan distance of 3d 
        // cube -> because a hexagon is just the diagonal of a cube.
        return (Math.Abs(a.q - b.q) + Math.Abs(a.r- b.r) + Math.Abs(a.s - b.s)) / 2;
    }

    Cube oddr_to_cube(Odd_R hex)
    {
        int parity = hex.row & 1;
        int q = hex.col - (hex.row - parity) / 2;
        int r = hex.row;
        return new Cube(q, r, -q-r);
    }
    
    Odd_R cube_to_oddr(Cube hex)
    {
        int parity = hex.r & 1;
        int col = hex.q + (hex.r - parity) / 2;
        int row = hex.r;
        return new Odd_R(col, row);
    }
}
