using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Notifications;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Interfaces;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetCurrentUserSummaryAsync_ShouldReturnUnreadRowsForCurrentUser()
    {
        var fixture = CreateFixture(7);
        fixture.NotificationRepository
            .Setup(r => r.GetForUserAsync(7, 5))
            .ReturnsAsync((
                new List<UserNotificationRow>
                {
                    new()
                    {
                        UserNotificationId = 11,
                        NotificationId = 22,
                        Type = "PackingIncident",
                        Severity = "Warning",
                        Title = "Nowe zgłoszenie",
                        Message = "Uszkodzona torba",
                        LinkUrl = "/admin/packing-incidents",
                        IsRead = false,
                        DeliveredAt = DateTimeOffset.UtcNow,
                    },
                },
                3));

        var result = await fixture.Service.GetCurrentUserSummaryAsync();

        result.UnreadCount.Should().Be(3);
        result.Items.Should().ContainSingle(item =>
            item.UserNotificationId == 11
            && item.Title == "Nowe zgłoszenie"
            && !item.IsRead);
    }

    [Fact]
    public async Task GetCurrentUserNotificationsAsync_ShouldNormalizeFilterAndMapPage()
    {
        var fixture = CreateFixture(7);
        UserNotificationQuery? capturedQuery = null;
        fixture.NotificationRepository
            .Setup(r => r.GetPageForUserAsync(7, It.IsAny<UserNotificationQuery>()))
            .Callback<int, UserNotificationQuery>((_, query) => capturedQuery = query)
            .ReturnsAsync(new UserNotificationPage
            {
                Items = new List<UserNotificationRow>
                {
                    new()
                    {
                        UserNotificationId = 15,
                        NotificationId = 25,
                        Type = "PackingIncident",
                        Severity = "Danger",
                        Title = "Awaria",
                        Message = "Wymaga reakcji",
                        IsRead = true,
                        DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                        ReadAt = DateTimeOffset.UtcNow,
                    },
                },
                TotalCount = 1,
                UnreadCount = 0,
                Page = 1,
                PageSize = 100,
            });

        var result = await fixture.Service.GetCurrentUserNotificationsAsync(new NotificationListFilterDto
        {
            Page = 0,
            PageSize = 999,
            Status = "Unread",
            Severity = " Danger ",
            Type = " PackingIncident ",
            Search = " awaria ",
        });

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(100);
        capturedQuery.Status.Should().Be("Unread");
        capturedQuery.Severity.Should().Be("Danger");
        capturedQuery.Type.Should().Be("PackingIncident");
        capturedQuery.Search.Should().Be("awaria");
        result.Items.Should().ContainSingle(item => item.UserNotificationId == 15 && item.ReadAt.HasValue);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task MarkManyAsReadAsync_ShouldPassDistinctPositiveIdsForCurrentUser()
    {
        var fixture = CreateFixture(7);
        IReadOnlyCollection<long>? capturedIds = null;
        fixture.NotificationRepository
            .Setup(r => r.MarkManyAsReadAsync(7, It.IsAny<IReadOnlyCollection<long>>()))
            .Callback<int, IReadOnlyCollection<long>>((_, ids) => capturedIds = ids)
            .ReturnsAsync(2);

        var result = await fixture.Service.MarkManyAsReadAsync(new long[] { 5, 0, 5, -1, 9 });

        result.Should().Be(2);
        capturedIds.Should().BeEquivalentTo(new long[] { 5, 9 });
    }

    [Fact]
    public async Task ArchiveManyAsync_ShouldArchiveOnlyCurrentUsersSelectedNotifications()
    {
        var fixture = CreateFixture(7);
        IReadOnlyCollection<long>? capturedIds = null;
        fixture.NotificationRepository
            .Setup(r => r.ArchiveManyAsync(7, It.IsAny<IReadOnlyCollection<long>>()))
            .Callback<int, IReadOnlyCollection<long>>((_, ids) => capturedIds = ids)
            .ReturnsAsync(2);

        var result = await fixture.Service.ArchiveManyAsync(new long[] { 3, 3, 4, 0 });

        result.Should().Be(2);
        capturedIds.Should().BeEquivalentTo(new long[] { 3, 4 });
    }

    [Fact]
    public async Task ArchiveReadAsync_ShouldNotCallRepository_WhenUserIsAnonymous()
    {
        var fixture = CreateFixture(null);

        var result = await fixture.Service.ArchiveReadAsync();

        result.Should().Be(0);
        fixture.NotificationRepository.Verify(r => r.ArchiveReadAsync(It.IsAny<int>()), Times.Never);
    }

    private static NotificationServiceFixture CreateFixture(int? userId)
    {
        var notificationRepository = new Mock<INotificationRepository>();
        var userRepository = new Mock<IUserRepository>();
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.Setup(s => s.GetUserId()).Returns(userId);

        return new NotificationServiceFixture(
            new NotificationService(notificationRepository.Object, userRepository.Object, currentUserService.Object),
            notificationRepository);
    }

    private sealed record NotificationServiceFixture(
        NotificationService Service,
        Mock<INotificationRepository> NotificationRepository);
}
