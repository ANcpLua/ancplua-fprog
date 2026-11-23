namespace Demo.Fp0502_CastHidesCases;

public union Choice(string, int);

// Correct: the switch is on the union value, so each label tests the case it holds.
public static class Tags
{
    public static int Tag(Choice choice)
    {
        switch (choice)
        {
            case string: return 1;
            case int: return 2;
        }

        return 0;
    }
}
