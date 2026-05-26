using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Shared.Middleware.Exceptions;

namespace Shared.ImageService
{
    /// <summary>
    /// Local disk image storage implementation.
    /// Stores images in wwwroot/uploads/{category}/{guid}.{ext}
    /// and serves them as static files.
    /// </summary>
    public class LocalImageService : IImageService
    {
        private readonly string _uploadRoot;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
        };

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"
        };

        public LocalImageService(IWebHostEnvironment env)
        {
            _uploadRoot = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
            Directory.CreateDirectory(_uploadRoot);
        }

        public async Task<string> UploadAsync(IFormFile file, string category)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("No file provided.");

            if (file.Length > MaxFileSizeBytes)
                throw new BadRequestException($"File size exceeds the {MaxFileSizeBytes / (1024 * 1024)}MB limit.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new BadRequestException($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                throw new BadRequestException($"MIME type '{file.ContentType}' is not allowed.");

            // Sanitized file name with GUID
            var categoryDir = Path.Combine(_uploadRoot, category);
            Directory.CreateDirectory(categoryDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(categoryDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Return relative path for storage in DB
            return $"uploads/{category}/{fileName}";
        }

        public Task<bool> DeleteAsync(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return Task.FromResult(false);

            var fullPath = Path.Combine(
                _uploadRoot.Replace("uploads", ""),
                imagePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public string GetImageUrl(string imagePath, HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return string.Empty;

            return $"{request.Scheme}://{request.Host}/{imagePath}";
        }
    }
}
