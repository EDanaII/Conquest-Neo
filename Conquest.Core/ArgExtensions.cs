namespace Conquest.Core;

public static class ArgExtensions {
    // Accepts: "--key 123", "--key=123"
    public static int GetInt(this string[] args, string key, int @default) {
        if (args is null || args.Length == 0) return @default;

        for (int i = 0; i < args.Length; i++) {
            var a = args[i];
            if (a.StartsWith(key, StringComparison.OrdinalIgnoreCase)) {
                // "--key=123"
                var eq = a.IndexOf('=');
                if (eq >= 0 && eq + 1 < a.Length) {
                    if (int.TryParse(a.AsSpan(eq + 1), out var v)) return v;
                    return @default;
                }
                // "--key 123"
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var v2)) return v2;
                return @default;
            }
        }
        return @default;
    }

    // String version if you ever need it
    public static string GetString(this string[] args, string key, string @default) {
        if (args is null || args.Length == 0) return @default;

        for (int i = 0; i < args.Length; i++) {
            var a = args[i];
            if (a.StartsWith(key, StringComparison.OrdinalIgnoreCase)) {
                var eq = a.IndexOf('=');
                if (eq >= 0 && eq + 1 < a.Length) return a[(eq + 1)..];
                if (i + 1 < args.Length) return args[i + 1];
                return @default;
            }
        }
        return @default;
    }
}
