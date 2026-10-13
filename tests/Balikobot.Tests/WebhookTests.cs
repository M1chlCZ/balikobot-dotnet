using System.Text;

namespace Balikobot.Tests;

public class WebhookTests
{
    [Fact]
    public void ParseTrackingNotificationsAcceptsASingleObject()
    {
        var notifications = BalikobotClient.ParseTrackingNotifications(
            """
            {
                "package_id": "add-ppl-8728035",
                "eid": "order-2026-000123-S1",
                "carrier_id": "DR1536622512M",
                "status_id": 1,
                "status_id_v2": 1.2,
                "name": "Zásilka byla doručena příjemci.",
                "status_text": "Doručeno"
            }
            """u8.ToArray());

        var notification = Assert.Single(notifications);
        Assert.Equal("add-ppl-8728035", notification.PackageId);
        Assert.Equal("order-2026-000123-S1", notification.Eid);
        Assert.Equal("DR1536622512M", notification.CarrierId);
        Assert.Equal("1.2", notification.StatusId);
        Assert.Equal("Zásilka byla doručena příjemci.", notification.StatusText);
    }

    [Fact]
    public void ParseTrackingNotificationsAcceptsAnArray()
    {
        var notifications = BalikobotClient.ParseTrackingNotifications(
            """
            [
                {
                    "carrier_id": "TRACK-1",
                    "status_id": 2,
                    "status_text": "Zásilka v přepravě."
                },
                {
                    "package_id": 42,
                    "eid": 12345678,
                    "carrier_id": "TRACK-2",
                    "status_id_v2": 2.2,
                    "name": "Zásilka byla převzata do přepravy."
                },
                {
                    "carrier_id": "TRACK-3",
                    "status_id": -1,
                    "name": "Zásilka zatím nebyla předána dopravci."
                }
            ]
            """u8.ToArray());

        Assert.Equal(3, notifications.Count);
        var first = notifications[0];
        Assert.Equal("TRACK-1", first.CarrierId);
        Assert.Equal(string.Empty, first.PackageId);
        Assert.Equal(string.Empty, first.Eid);
        Assert.Equal("2", first.StatusId);
        Assert.Equal("Zásilka v přepravě.", first.StatusText);

        var second = notifications[1];
        Assert.Equal("42", second.PackageId);
        Assert.Equal("12345678", second.Eid);
        Assert.Equal("TRACK-2", second.CarrierId);
        Assert.Equal("2.2", second.StatusId);
        Assert.Equal("Zásilka byla převzata do přepravy.", second.StatusText);

        var third = notifications[2];
        Assert.Equal("-1", third.StatusId);
        Assert.Equal("Zásilka zatím nebyla předána dopravci.", third.StatusText);
    }

    [Fact]
    public void ParseTrackingNotificationsTreatsNullIdentifiersAsAbsent()
    {
        var notifications = BalikobotClient.ParseTrackingNotifications(
            """
            {
                "package_id": null,
                "eid": null,
                "carrier_id": "TRACK-1",
                "status_id": 2,
                "status_text": "Zásilka v přepravě."
            }
            """u8.ToArray());

        var notification = Assert.Single(notifications);
        Assert.Equal(string.Empty, notification.PackageId);
        Assert.Equal(string.Empty, notification.Eid);
        Assert.Equal("TRACK-1", notification.CarrierId);
    }

    [Fact]
    public void ParseTrackingNotificationsAcceptsAnEmptyArray()
    {
        var notifications = BalikobotClient.ParseTrackingNotifications("[]"u8.ToArray());

        Assert.Empty(notifications);
    }

    [Fact]
    public void ParseTrackingNotificationsAcceptsOneThousandEntries()
    {
        var body = new StringBuilder("[");
        for (var index = 0; index < 1000; index++)
        {
            if (index > 0)
            {
                body.Append(',');
            }

            body.Append("{\"carrier_id\":\"TRACK-").Append(index).Append("\",\"status_id\":2,\"name\":\"Transit\"}");
        }

        body.Append(']');

        var notifications = BalikobotClient.ParseTrackingNotifications(Encoding.UTF8.GetBytes(body.ToString()));

        Assert.Equal(1000, notifications.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\n ")]
    [InlineData("null")]
    [InlineData("\"status\"")]
    [InlineData("{\"carrier_id\":")]
    [InlineData("""{"status_id":1,"name":"Delivered"}""")]
    [InlineData("{\"package_id\":\"\",\"eid\":\"\",\"carrier_id\":\"\",\"status_id\":1,\"name\":\"Delivered\"}")]
    [InlineData("{\"package_id\":null,\"eid\":null,\"carrier_id\":null,\"status_id\":1,\"name\":\"Delivered\"}")]
    [InlineData("""{"carrier_id":"TRACK-1","name":"Delivered"}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":"1.2","name":"Delivered"}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":1.2345,"name":"Delivered"}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":1,"status_id_v2":null,"name":"Delivered"}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":1}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":1,"name":"","status_text":""}""")]
    [InlineData("""{"carrier_id":"TRACK-1","status_id":1,"name":"Delivered\nNow"}""")]
    [InlineData("""{"carrier_id":true,"status_id":1,"name":"Delivered"}""")]
    [InlineData("""{"carrier_id":{},"status_id":1,"name":"Delivered"}""")]
    [InlineData("[1]")]
    [InlineData("[null]")]
    [InlineData("""
        [
            {"carrier_id":"TRACK-1","status_id":1,"name":"Delivered"},
            {"status_id":1,"name":"Delivered"}
        ]
        """)]
    public void ParseTrackingNotificationsRejectsMalformedInput(string body)
    {
        var exception = Assert.Throws<BalikobotException>(
            () => BalikobotClient.ParseTrackingNotifications(Encoding.UTF8.GetBytes(body)));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public void ParseTrackingNotificationsRejectsOverlongFields()
    {
        var cases = new[]
        {
            "{\"carrier_id\":\"" + new string('x', 101) + "\",\"status_id\":1,\"name\":\"Delivered\"}",
            "{\"carrier_id\":" + new string('1', 101) + ",\"status_id\":1,\"name\":\"Delivered\"}",
            "{\"carrier_id\":\"TRACK-1\",\"status_id\":1,\"name\":\"" + new string('a', 201) + "\"}",
        };

        foreach (var body in cases)
        {
            var exception = Assert.Throws<BalikobotException>(
                () => BalikobotClient.ParseTrackingNotifications(Encoding.UTF8.GetBytes(body)));

            Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
        }
    }

    [Fact]
    public void ParseTrackingNotificationsRejectsAnOversizedArray()
    {
        var body = new StringBuilder("[");
        for (var index = 0; index <= 1000; index++)
        {
            if (index > 0)
            {
                body.Append(',');
            }

            body.Append("{\"carrier_id\":\"TRACK-").Append(index).Append("\",\"status_id\":2,\"name\":\"Transit\"}");
        }

        body.Append(']');

        var exception = Assert.Throws<BalikobotException>(
            () => BalikobotClient.ParseTrackingNotifications(Encoding.UTF8.GetBytes(body.ToString())));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }
}
