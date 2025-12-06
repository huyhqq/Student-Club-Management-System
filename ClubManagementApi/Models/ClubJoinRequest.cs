using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class ClubJoinRequest
{
    [Key]
    [Column("RequestID")]
    public int RequestId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column("ClubID")]
    public int ClubId { get; set; }

    [Column("StudentID")]
    [StringLength(50)]
    public string StudentId { get; set; } = null!;

    [StringLength(200)]
    public string Major { get; set; } = null!;

    [StringLength(50)]
    public string AcademicYear { get; set; } = null!;

    public string Introduction { get; set; } = null!;

    public string Reason { get; set; } = null!;

    [StringLength(200)]
    public string? ContactInfoOptional { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey("ClubId")]
    [InverseProperty("ClubJoinRequests")]
    public virtual Club Club { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ClubJoinRequests")]
    public virtual User User { get; set; } = null!;
}
