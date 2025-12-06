using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class ClubMember
{
    [Key]
    [Column("MemberID")]
    public int MemberId { get; set; }

    [Column("ClubID")]
    public int ClubId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? JoinedDate { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [ForeignKey("ClubId")]
    [InverseProperty("ClubMembers")]
    public virtual Club Club { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ClubMembers")]
    public virtual User User { get; set; } = null!;
}
