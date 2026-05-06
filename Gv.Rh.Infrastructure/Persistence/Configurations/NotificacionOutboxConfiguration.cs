using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class NotificacionOutboxConfiguration : IEntityTypeConfiguration<NotificacionOutbox>
{
    public void Configure(EntityTypeBuilder<NotificacionOutbox> builder)
    {
        builder.ToTable("notificacion_outbox");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Canal)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.DestinatariosJson)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.HtmlBody)
            .IsRequired();

        builder.Property(x => x.Estado)
            .HasConversion(
                v => v.ToString(),
                v => string.IsNullOrWhiteSpace(v)
                    ? EstatusNotificacionOutbox.PENDIENTE
                    : Enum.Parse<EstatusNotificacionOutbox>(v, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.UltimoError)
            .HasMaxLength(4000);

        builder.Property(x => x.CorrelationType)
            .HasMaxLength(120);

        builder.Property(x => x.NextAttemptUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SentAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.Estado);
        builder.HasIndex(x => x.NextAttemptUtc);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.CorrelationType, x.CorrelationId });
    }
}