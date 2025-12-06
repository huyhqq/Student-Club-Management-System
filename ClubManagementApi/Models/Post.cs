using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class Post
{
    [Key]
    [Column("PostID")]
    public int PostId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column("ClubID")]
    public int? ClubId { get; set; }

    public string? Content { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [StringLength(20)]
    public string? Visibility { get; set; }

    [ForeignKey("ClubId")]
    [InverseProperty("Posts")]
    public virtual Club? Club { get; set; }

    [InverseProperty("Post")]
    public virtual ICollection<PostImage> PostImages { get; set; } = new List<PostImage>();

    [ForeignKey("UserId")]
    [InverseProperty("Posts")]
    public virtual User User { get; set; } = null!;
}
