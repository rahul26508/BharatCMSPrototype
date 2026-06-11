using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace BharatCMS.Infrastructure.Services;

/// <summary>
/// File validation using magic numbers - not extensions.
/// CERT-In: Prevents malicious file execution attempts.
/// </summary>
public class FileValidationService
{
    private readonly ILogger<FileValidationService> _logger;

    // Magic byte signatures for common file types
    private static readonly Dictionary<string, FileSignature> KnownSignatures = new()
    {
        { "pdf", new FileSignature("PDF", new byte[][] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, "application/pdf") },
        { "docx", new FileSignature("DOCX", new byte[][] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document") },
        { "xlsx", new FileSignature("XLSX", new byte[][] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") },
        { "png", new FileSignature("PNG", new byte[][] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }, "image/png") },
        { "jpg", new FileSignature("JPG", new byte[][] { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 } }, "image/jpeg") },
        { "gif", new FileSignature("GIF", new byte[][] { new byte[] { 0x47, 0x49, 0x46, 0x38 } }, "image/gif") },
        { "mp4", new FileSignature("MP4", new byte[][] { new byte[] { 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D } }, "video/mp4") },
        { "mp3", new FileSignature("MP3", new byte[][] { new byte[] { 0xFF, 0xFB }, new byte[] { 0x49, 0x44, 0x33 } }, "audio/mpeg") }
    };

    public FileValidationService(ILogger<FileValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate file using magic bytes, not extension.
    /// </summary>
    public async Task<FileValidationResult> ValidateAsync(IFormFile file)
    {
        // Check file size (max 50MB)
        if (file.Length > 50 * 1024 * 1024)
        {
            return new FileValidationResult { IsValid = false, Error = "File size exceeds 50MB limit" };
        }

        // Read first 8KB for magic byte detection
        var buffer = new byte[8192];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(buffer);
        var magicBytes = buffer.Take(bytesRead).ToArray();

        // Calculate checksum
        _ = stream.Position;
        stream.Position = 0;
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        var checksum = Convert.ToHexString(hash);

        // Detect actual file type
        var detectedType = DetectFileType(magicBytes);
        
        if (detectedType == null)
        {
            return new FileValidationResult { IsValid = false, Error = "Unable to identify file type" };
        }

        // Check against allowed types
        var allowedTypes = new[] { "pdf", "docx", "xlsx", "png", "jpg", "gif", "mp4", "mp3" };
        if (!allowedTypes.Contains(detectedType))
        {
            return new FileValidationResult { IsValid = false, Error = "File type not allowed" };
        }

        // Check for executable signatures
        if (ContainsExecutableSignature(magicBytes))
        {
            _logger.LogWarning("Executable content detected in uploaded file: {Filename}", file.FileName);
            return new FileValidationResult { IsValid = false, Error = "File contains executable content" };
        }

        return new FileValidationResult
        {
            IsValid = true,
            DetectedType = detectedType,
            DetectedContentType = KnownSignatures[detectedType].ContentType,
            MagicBytes = Convert.ToHexString(magicBytes.Take(16).ToArray()),
            Checksum = checksum
        };
    }

    private string? DetectFileType(byte[] bytes)
    {
        foreach (var (type, signature) in KnownSignatures)
        {
            foreach (var pattern in signature.Patterns)
            {
                if (bytes.Length >= pattern.Length && bytes.Take(pattern.Length).SequenceEqual(pattern))
                {
                    return type;
                }
            }
        }
        return null;
    }

    private bool ContainsExecutableSignature(byte[] bytes)
    {
        // Check for PE executable
        if (bytes.Length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A) return true; // MZ
        if (bytes.Length >= 4 && bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46) return true; // ELF
        if (bytes.Length >= 4 && bytes[0] == 0xCA && bytes[1] == 0xFE && bytes[2] == 0xBA && bytes[3] == 0xBE) return true; // Java class
        return false;
    }

    /// <summary>
    /// Generate secure filename with random prefix.
    /// </summary>
    public string GenerateSecureFilename(string originalFilename)
    {
        // Get extension from filename (never trust the extension!)
        // The actual type is determined by magic bytes
        var ext = Path.GetExtension(originalFilename);
        
        // Generate random prefix + keep extension (for display purposes only)
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        
        return $"{timestamp}_{random}{ext}";
    }
}

public class FileSignature
{
    public string Name { get; }
    public byte[][] Patterns { get; }
    public string ContentType { get; }

    public FileSignature(string name, byte[][] patterns, string contentType)
    {
        Name = name;
        Patterns = patterns;
        ContentType = contentType;
    }
}

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string? DetectedType { get; set; }
    public string? DetectedContentType { get; set; }
    public string? MagicBytes { get; set; }
    public string? Checksum { get; set; }
}
