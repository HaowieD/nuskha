namespace Nuskha.Web.Services;

/// <summary>Stores user/editor uploaded images (covers, avatars) on local disk after validating real file type and size.</summary>
public class UploadService(IConfiguration config, IWebHostEnvironment env)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public string RootPath
    {
        get
        {
            var configured = config["Uploads:Path"];
            return string.IsNullOrWhiteSpace(configured) ? Path.Combine(env.ContentRootPath, "uploads") : configured;
        }
    }

    /// <summary>Returns the stored relative path (e.g. "covers/abc.jpg") or null when the file is not an acceptable image.</summary>
    public async Task<string?> SaveImageAsync(IFormFile? file, string folder, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0 || file.Length > MaxBytes) return null;

        var header = new byte[12];
        await using (var probe = file.OpenReadStream())
        {
            var read = 0;
            while (read < header.Length)
            {
                var n = await probe.ReadAsync(header.AsMemory(read), ct);
                if (n == 0) break;
                read += n;
            }
        }

        string? ext = null;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) ext = ".jpg";
        else if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) ext = ".png";
        else if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                 && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P') ext = ".webp";
        if (ext is null) return null;

        var dir = Path.Combine(RootPath, folder);
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext;
        await using (var target = File.Create(Path.Combine(dir, name)))
        await using (var source = file.OpenReadStream())
            await source.CopyToAsync(target, ct);
        return $"{folder}/{name}";
    }

    public void Delete(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return;
        var root = Path.GetFullPath(RootPath);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full))
            File.Delete(full);
    }
}
