﻿using EntityFrameworkCore.Vfp.Tests.Data.Northwind.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Vfp.Tests.Data.Northwind.Configurations {
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier> {
        public void Configure(EntityTypeBuilder<Supplier> builder) {
            builder.OwnsOne(x => x.Address, a => {
                a.Property(p => p.Address).HasColumnName("Address");
                a.Property(p => p.City).HasColumnName("City");
                a.Property(p => p.Country).HasColumnName("Country");
                a.Property(p => p.Region).HasColumnName("Region");
            });
        }
    }
}
