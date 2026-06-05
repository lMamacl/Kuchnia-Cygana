// <copyright file="MigrationVersionTests.cs" company="KuchniaUCygana">
// Copyright (c) KuchniaUCygana. All rights reserved.
// </copyright>

using System.Reflection;
using FluentAssertions;
using FluentMigrator;
using KuchniaUCygana.Infrastructure.Persistence.Migrations;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class MigrationVersionTests
{
    [Fact]
    public void InfrastructureMigrations_ShouldHaveUniqueVersions()
    {
        var duplicateVersions = typeof(MigrationRunner).Assembly
            .GetTypes()
            .Select(type => new
            {
                Type = type,
                Migration = type.GetCustomAttribute<MigrationAttribute>(),
            })
            .Where(item => item.Migration is not null)
            .GroupBy(item => item.Migration!.Version)
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Version = group.Key,
                Names = group.Select(item => item.Type.Name).Order(StringComparer.Ordinal).ToArray(),
            })
            .ToArray();

        duplicateVersions.Should().BeEmpty();
    }
}
