namespace CertTool.Models.Dto
{
    public class DigDirJwk
    {
        public required string Kty { get; set; }
        public required string N { get; set; }
        public string? E { get; set; }
        public required string Alg { get; set; }
        public required string Kid { get; set; }
        public required string Use { get; set; }
    }
}
