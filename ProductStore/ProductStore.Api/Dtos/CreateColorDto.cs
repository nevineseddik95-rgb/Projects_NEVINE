using System.ComponentModel.DataAnnotations;

namespace productStore.Api.Dtos;

public record class CreateColorDto([Required]String Name);
