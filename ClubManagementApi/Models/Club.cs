using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class Club
{
    [Key]
    [Column("ClubID")]
    public int ClubId { get; set; }

    [StringLength(100)]
    public string ClubName { get; set; } = null!;

    public string? Description { get; set; }

    [Column("PresidentID")]
    public int? PresidentId { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("Club")]
    public virtual ICollection<ClubJoinRequest> ClubJoinRequests { get; set; } = new List<ClubJoinRequest>();

    [InverseProperty("Club")]
    public virtual ICollection<ClubMember> ClubMembers { get; set; } = new List<ClubMember>();

    [InverseProperty("Club")]
    public virtual ICollection<FeeSchedule> FeeSchedules { get; set; } = new List<FeeSchedule>();

    [InverseProperty("Club")]
    public virtual ICollection<Fee> Fees { get; set; } = new List<Fee>();

    [InverseProperty("Club")]
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    [ForeignKey("PresidentId")]
    [InverseProperty("Clubs")]
    public virtual User? President { get; set; }
}
