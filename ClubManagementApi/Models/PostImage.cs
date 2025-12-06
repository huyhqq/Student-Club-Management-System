using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class PostImage
{
    [Key]
    [Column("ImageID")]
    public int ImageId { get; set; }

    [Column("PostID")]
    public int PostId { get; set; }

    [Column("ImageURL")]
    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("PostId")]
    [InverseProperty("PostImages")]
    public virtual Post Post { get; set; } = null!;
}
