using Microsoft.AspNetCore.Http;

namespace Shared.ImageService
{
    /// <summary>
    /// Abstraction for image upload, deletion, and URL generation.
    /// Implementations can target local disk, Azure Blob, or S3.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Uploads an image file and returns the relative path for storage.
        /// </summary>
        /// <param name="file">The uploaded file</param>
        /// <param name="category">Category folder (e.g. "profiles", "flights")</param>
        /// <returns>Relative path to stored image</returns>
        Task<string> UploadAsync(IFormFile file, string category);

        /// <summary>
        /// Deletes an image by its relative path.
        /// </summary>
        Task<bool> DeleteAsync(string imagePath);

        /// <summary>
        /// Gets the public URL for an image path.
        /// </summary>
        string GetImageUrl(string imagePath, HttpRequest request);
    }
}
