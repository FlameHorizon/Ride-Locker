public static class WebHostEnvironmentExtensions {
  public static string GetRelativeWebPath(this IWebHostEnvironment env, string physicalPath) {
    return "/" + Path.GetRelativePath(env.WebRootPath, physicalPath).Replace("\\", "/");
  }
}