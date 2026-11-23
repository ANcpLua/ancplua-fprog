namespace Demo.Fp0502_CastHidesCases;

public union Choice(string, int);

// Bug: behind the cast the labels test the union itself, so neither label is ever taken.
public static class Tags
{
    public static int Tag(Choice choice)
    {
        switch ((object)choice) // expect: FP0502
        {
            case string: return 1;
            case int: return 2;
        }

        return 0;
    }
}
