using System;
using System.Collections.Generic;
using ClubManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Data;

public partial class StudentClubContext : DbContext
{
    public StudentClubContext()
    {
    }

    public StudentClubContext(DbContextOptions<StudentClubContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Club> Clubs { get; set; }

    public virtual DbSet<ClubJoinRequest> ClubJoinRequests { get; set; }

    public virtual DbSet<ClubMember> ClubMembers { get; set; }

    public virtual DbSet<Fee> Fees { get; set; }

    public virtual DbSet<FeeSchedule> FeeSchedules { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostImage> PostImages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserToken> UserTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Club>(entity =>
        {
            entity.HasKey(e => e.ClubId).HasName("PK__Clubs__D35058C79B4A1DB3");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.President).WithMany(p => p.Clubs).HasConstraintName("FK__Clubs__President__47DBAE45");
        });

        modelBuilder.Entity<ClubJoinRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__ClubJoin__33A8519A9DC40B07");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Club).WithMany(p => p.ClubJoinRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClubJoinR__ClubI__123EB7A3");

            entity.HasOne(d => d.User).WithMany(p => p.ClubJoinRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClubJoinR__UserI__114A936A");
        });

        modelBuilder.Entity<ClubMember>(entity =>
        {
            entity.HasKey(e => e.MemberId).HasName("PK__ClubMemb__0CF04B380ABAC985");

            entity.Property(e => e.JoinedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Club).WithMany(p => p.ClubMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClubMembe__ClubI__4D94879B");

            entity.HasOne(d => d.User).WithMany(p => p.ClubMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClubMembe__UserI__4E88ABD4");
        });

        modelBuilder.Entity<Fee>(entity =>
        {
            entity.HasKey(e => e.FeeId).HasName("PK__Fees__B387B2090C25BC1C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Pending");

            entity.HasOne(d => d.Club).WithMany(p => p.Fees)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Fees__ClubID__619B8048");

            entity.HasOne(d => d.FeeSchedule).WithMany(p => p.Fees)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Fees__FeeSchedul__60A75C0F");

            entity.HasOne(d => d.User).WithMany(p => p.Fees)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Fees__UserID__628FA481");
        });

        modelBuilder.Entity<FeeSchedule>(entity =>
        {
            entity.HasKey(e => e.FeeScheduleId).HasName("PK__FeeSched__C0F7BAD038719E89");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.Club).WithMany(p => p.FeeSchedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FeeSchedu__ClubI__59FA5E80");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E328C9556B7");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__6EF57B66");
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__Posts__AA1260387B4BB7AC");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Visibility).HasDefaultValue("Public");

            entity.HasOne(d => d.Club).WithMany(p => p.Posts).HasConstraintName("FK__Posts__ClubID__06CD04F7");

            entity.HasOne(d => d.User).WithMany(p => p.Posts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Posts__UserID__05D8E0BE");
        });

        modelBuilder.Entity<PostImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__PostImag__7516F4EC7D8AC026");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Post).WithMany(p => p.PostImages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostImage__PostI__0A9D95DB");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCACB2D902CE");

            entity.Property(e => e.AccountStatus).HasDefaultValue("PendingVerification");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__UserToke__658FEE8A52DD88B9");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsUsed).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.UserTokens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserToken__UserI__4222D4EF");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
