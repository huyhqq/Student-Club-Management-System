using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Models;

public partial class Fee
{
    [Key]
    [Column("FeeID")]
    public int FeeId { get; set; }

    [Column("FeeScheduleID")]
    public int FeeScheduleId { get; set; }

    [Column("ClubID")]
    public int ClubId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    public int OrderCode { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string? FeeType { get; set; }

    [StringLength(20)]
    public string? PaymentStatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? PaidAt { get; set; }

    [ForeignKey("ClubId")]
    [InverseProperty("Fees")]
    public virtual Club Club { get; set; } = null!;

    [ForeignKey("FeeScheduleId")]
    [InverseProperty("Fees")]
    public virtual FeeSchedule FeeSchedule { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("Fees")]
    public virtual User User { get; set; } = null!;
}
