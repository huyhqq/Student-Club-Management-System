using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

[Index("Email", Name = "UQ__Users__A9D10534246D279E", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [StringLength(100)]
    public string Email { get; set; } = null!;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(10)]
    public string? StudentCode { get; set; }

    public string? Avatar { get; set; }

    [MaxLength(256)]
    public byte[] PasswordHash { get; set; } = null!;

    [StringLength(20)]
    public string Role { get; set; } = null!;

    [StringLength(20)]
    public string? AccountStatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastLogin { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<ClubJoinRequest> ClubJoinRequests { get; set; } = new List<ClubJoinRequest>();

    [InverseProperty("User")]
    public virtual ICollection<ClubMember> ClubMembers { get; set; } = new List<ClubMember>();

    [InverseProperty("President")]
    public virtual ICollection<Club> Clubs { get; set; } = new List<Club>();

    [InverseProperty("User")]
    public virtual ICollection<Fee> Fees { get; set; } = new List<Fee>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("User")]
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    [InverseProperty("User")]
    public virtual ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
}
