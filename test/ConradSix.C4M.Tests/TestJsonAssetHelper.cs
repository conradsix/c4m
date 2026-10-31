namespace ConradSix.C4M.Tests;

public static class TestJsonAssetHelper
{
    public static string LoadJsonString(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "json", filename));
}
