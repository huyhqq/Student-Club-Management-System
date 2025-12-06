using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

[Table("FeeSchedule")]
public partial class FeeSchedule
{
    [Key]
    [Column("FeeScheduleID")]
    public int FeeScheduleId { get; set; }

    [Column("ClubID")]
    public int ClubId { get; set; }

    [StringLength(200)]
    public string FeeName { get; set; } = null!;

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    [StringLength(20)]
    public string? Frequency { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    public bool IsRequiredFee { get; set; }

    [ForeignKey("ClubId")]
    [InverseProperty("FeeSchedules")]
    public virtual Club Club { get; set; } = null!;

    [InverseProperty("FeeSchedule")]
    public virtual ICollection<Fee> Fees { get; set; } = new List<Fee>();
}
