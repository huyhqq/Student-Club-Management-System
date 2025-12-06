using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

[Index("Token", Name = "UQ__UserToke__1EB4F817B4E3CB5F", IsUnique = true)]
public partial class UserToken
{
    [Key]
    [Column("TokenID")]
    public int TokenId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(200)]
    public string Token { get; set; } = null!;

    [StringLength(50)]
    public string? TokenType { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ExpiryDate { get; set; }

    public bool? IsUsed { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserTokens")]
    public virtual User User { get; set; } = null!;
}
