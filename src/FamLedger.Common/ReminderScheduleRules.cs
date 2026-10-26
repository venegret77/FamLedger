using System.Text.Json;
using System.Text.Json.Serialization;
using FamLedger.Domain.Models;

namespace FamLedger.Common;

public static class ReminderScheduleRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new TimeOnlyHmConverter() },
    };

    public static ReminderSchedule EveryDay(TimeOnly timeUtc)
    {
        return new ReminderSchedule
        {
            SameTimeEveryDay = true,
            Days = Enumerable.Range(1, 7)
                .Select(day => new ReminderDaySlot
                {
                    Day = day,
                    Enabled = true,
                    TimeUtc = timeUtc,
                })
                .ToList(),
        };
    }

    public static ReminderSchedule Normalize(ReminderSchedule input)
    {
        var enabled = new List<ReminderDaySlot>();
        foreach (var day in input.Days)
        {
            if (day.Day is < 1 or > 7)
                throw new InvalidOperationException("Day must be from 1 (Monday) to 7 (Sunday)");
            if (!day.Enabled)
                continue;

            enabled.Add(new ReminderDaySlot
            {
                Day = day.Day,
                Enabled = true,
                TimeUtc = new TimeOnly(day.TimeUtc.Hour, day.TimeUtc.Minute),
            });
        }

        if (enabled.Count == 0)
            throw new InvalidOperationException("At least one weekday is required");

        return new ReminderSchedule
        {
            SameTimeEveryDay = input.SameTimeEveryDay,
            Days = enabled
                .OrderBy(day => day.Day)
                .ThenBy(day => day.TimeUtc)
                .ToList(),
        };
    }

    public static string Serialize(ReminderSchedule schedule) =>
        JsonSerializer.Serialize(schedule, JsonOptions);

    public static ReminderSchedule? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ReminderSchedule>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ReminderSchedule? Resolve(string? json, TimeOnly? fallbackTimeUtc)
    {
        var parsed = Parse(json);
        if (parsed is { Days.Count: > 0 })
            return parsed;
        if (fallbackTimeUtc is null)
            return null;
        return EveryDay(fallbackTimeUtc.Value);
    }

    public static TimeOnly RepresentativeTime(ReminderSchedule schedule)
    {
        var first = schedule.Days.FirstOrDefault(day => day.Enabled);
        if (first is null)
            throw new InvalidOperationException("At least one weekday is required");
        return first.TimeUtc;
    }

    public static bool IsDue(
        string? scheduleJson,
        TimeOnly? fallbackTimeUtc,
        DateTime? lastFiredAtUtc,
        DateOnly? lastFiredDateUtc,
        DateTime utcNow)
    {
        var schedule = Resolve(scheduleJson, fallbackTimeUtc);
        if (schedule is null)
            return false;

        var isoDay = ToIsoWeekday(utcNow.DayOfWeek);
        var minute = new TimeOnly(utcNow.Hour, utcNow.Minute);
        var matches = schedule.Days.Any(day =>
            day.Enabled &&
            day.Day == isoDay &&
            day.TimeUtc.Hour == minute.Hour &&
            day.TimeUtc.Minute == minute.Minute);
        if (!matches)
            return false;

        return !AlreadyFired(lastFiredAtUtc, lastFiredDateUtc, utcNow, minute);
    }

    public static int ToIsoWeekday(DayOfWeek day) =>
        day == DayOfWeek.Sunday ? 7 : (int)day;

    private static bool AlreadyFired(
        DateTime? lastFiredAtUtc,
        DateOnly? lastFiredDateUtc,
        DateTime utcNow,
        TimeOnly slotTime)
    {
        var today = DateOnly.FromDateTime(utcNow);
        if (lastFiredAtUtc is null)
            return lastFiredDateUtc == today;

        var fired = lastFiredAtUtc.Value;
        if (fired.Kind == DateTimeKind.Local)
            fired = fired.ToUniversalTime();
        else if (fired.Kind == DateTimeKind.Unspecified)
            fired = DateTime.SpecifyKind(fired, DateTimeKind.Utc);

        var slotStart = today.ToDateTime(slotTime, DateTimeKind.Utc);
        return fired >= slotStart;
    }

    private sealed class TimeOnlyHmConverter : JsonConverter<TimeOnly>
    {
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text) || !TimeOnly.TryParse(text, out var time))
                throw new JsonException("Invalid time");
            return new TimeOnly(time.Hour, time.Minute);
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("HH:mm"));
    }
}
