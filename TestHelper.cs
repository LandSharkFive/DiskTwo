namespace UnitTestOne
{
    public static class TestHelper
    {
        public static string GetTempDb()
        {
            return Path.ChangeExtension(Path.GetRandomFileName(), "db");
        }

    }
}

