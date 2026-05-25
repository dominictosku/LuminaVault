namespace LuminaVault.Validation;

public static class FormFileValidation
{
    public static IResult? RequireExtension(
        IFormFile? file,
        string extension,
        string emptyMessage,
        string unsupportedTypeMessage)
    {
        if (file is null || file.Length == 0)
            return Problem.BadRequest(emptyMessage);

        return Path.GetExtension(file.FileName).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? null
            : Problem.BadRequest(unsupportedTypeMessage);
    }
}
