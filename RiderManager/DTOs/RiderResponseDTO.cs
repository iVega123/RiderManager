using System.ComponentModel.DataAnnotations;

namespace RiderManager.DTOs
{
    public class RiderResponseDTO
    {
        public required string Id { get; set; }

        public required string CNPJ { get; set; }

        public DateTime DataNascimento { get; set; }

        public required string NumeroCNH { get; set; }

        public required string TipoCNH { get; set; }

        public required string UserId { get; set; }

        public string? ImagemCNH { get; set; }
    }
}
