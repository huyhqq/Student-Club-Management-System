using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ClubManagementApi.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> config)
    {
        var settings = config.Value;
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<string> UploadAsync(IFormFile file, string folder = "club-management")
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ");

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, file.OpenReadStream()),
            Folder = folder,
            Transformation = new Transformation().Width(1200).Height(1200).Crop("limit")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        return uploadResult.SecureUrl.ToString();
    }
}
