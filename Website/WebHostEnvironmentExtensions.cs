public static class WebHostEnvironmentExtensions {
  public static string GetRelativeWebPath(this IWebHostEnvironment env, string path) {
    string res = path.Replace(env.WebRootPath, "").Replace("\\", "/");

    // Ensure leading slash
    if (!res.StartsWith("/")) {
      res = "/" + res;
    }

    return res;
  }
}