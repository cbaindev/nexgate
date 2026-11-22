using Nexgate.Domain.Access;
using Nexgate.Service.Access;
using Nexgate.Service.Access.Contracts;

namespace Nexgate.Test.Api;

/// <summary>Reglas de horarios, credenciales, compilación y desplazamientos, sin base de datos.</summary>
public class AccessUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Master = new byte[32];
    private static readonly Guid Site = Guid.NewGuid();
    private static readonly Guid Point = Guid.NewGuid();
    private static readonly IReadOnlyList<PolicyOffset> Cordoba = [new(null, -10800)];

    // --- Horarios ---

    [Fact]
    public void A_window_is_parsed_into_a_day_mask_and_minutes()
    {
        var window = ScheduleWindow.Parse(["monday", "friday"], "08:00", "24:00");

        Assert.Equal(new ScheduleWindow(1 | 16, 480, 1440), window);
        Assert.Equal(["monday", "friday"], window.DayNameList());
        Assert.Equal("24:00", ScheduleWindow.FormatTime(window.To));
    }

    [Theory]
    [InlineData(new string[0], "08:00", "17:00")]
    [InlineData(new[] { "lunes" }, "08:00", "17:00")]
    [InlineData(new[] { "monday" }, "17:00", "17:00")]
    [InlineData(new[] { "monday" }, "18:00", "17:00")]
    [InlineData(new[] { "monday" }, "08:00", "24:01")]
    [InlineData(new[] { "monday" }, "8:00", "17:00")]
    [InlineData(new[] { "monday" }, "08:60", "17:00")]
    public void Invalid_windows_are_rejected(string[] days, string from, string to)
    {
        var error = Assert.Throws<DomainValidationException>(() => ScheduleWindow.Parse(days, from, to));
        Assert.Equal("Windows", error.Field);
    }

    // --- Credenciales ---

    [Fact]
    public void Credential_hashes_depend_on_site_type_and_normalized_value()
    {
        var card = CredentialHash.Compute(Master, Site, CredentialType.Card, " ab12 ");

        Assert.Equal(card, CredentialHash.Compute(Master, Site, CredentialType.Card, "AB12"));
        Assert.NotEqual(card, CredentialHash.Compute(Master, Guid.NewGuid(), CredentialType.Card, "AB12"));
        Assert.NotEqual(card, CredentialHash.Compute(Master, Site, CredentialType.Rf, "AB12"));
        Assert.NotEqual(CredentialHash.Compute(Master, Site, CredentialType.Pin, "ab12"), CredentialHash.Compute(Master, Site, CredentialType.Pin, "AB12"));
        Assert.Equal(32, card.Length);
        Assert.DoesNotContain('=', CredentialHash.ToPolicy(card));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_credential_value_is_rejected(string value) =>
        Assert.Equal("Value", Assert.Throws<DomainValidationException>(() => CredentialHash.Normalize(CredentialType.Card, value)).Field);

    // --- Compilación ---

    private static CompiledPolicy Compile(IReadOnlyList<CompilableCredential> credentials, IReadOnlyList<CompilableGrant> grants) =>
        PolicyCompiler.Compile(Site, Point, "America/Argentina/Cordoba", Cordoba, credentials, grants, Now);

    [Fact]
    public void An_entry_is_valid_in_the_intersection_of_credential_and_grant()
    {
        var subject = Guid.NewGuid();
        var day = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var policy = Compile(
            [new(subject, "card", day, day.AddDays(19))],
            [new(subject, null, day.AddDays(9), day.AddDays(29), false, null)]);

        var entry = Assert.Single(policy.Entries);
        Assert.Equal(day.AddDays(9).ToUnixTimeSeconds(), entry.From);
        Assert.Equal(day.AddDays(19).ToUnixTimeSeconds(), entry.To);
        Assert.Equal(day.ToUnixTimeSeconds(), Assert.Single(policy.Credentials).From);
    }

    [Fact]
    public void Ended_grants_and_disjoint_validities_give_no_entries_but_active_credentials_stay()
    {
        var subject = Guid.NewGuid();
        var other = Guid.NewGuid();
        var policy = Compile(
            [new(subject, "card", null, Now.AddDays(1)), new(other, "otra", null, null), new(other, "vencida", null, Now.AddDays(-1))],
            [
                new(subject, null, null, Now.AddMinutes(-1), false, null),
                new(subject, null, Now.AddDays(2), null, false, null)
            ]);

        Assert.Empty(policy.Entries);
        Assert.Equal(["card", "otra"], policy.Credentials.Select(c => c.Hash));
    }

    [Fact]
    public void Deny_entries_have_no_uses_and_windows_are_shared()
    {
        var subject = Guid.NewGuid();
        IReadOnlyList<ScheduleWindow> weekdays = [new(31, 480, 1020)];
        var policy = Compile(
            [new(subject, "card", null, null)],
            [new(subject, weekdays, null, null, false, 3), new(subject, weekdays, null, null, true, null)]);

        Assert.Single(policy.Windows);
        Assert.Equal([new PolicyEntry(0, 0, null, null, 3, false), new PolicyEntry(0, 0, null, null, null, true)], policy.Entries);
    }

    [Fact]
    public void The_hash_is_deterministic_and_ignores_input_order()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        CompilableCredential[] credentials = [new(a, "aaa", null, null), new(b, "bbb", null, null)];
        CompilableGrant[] grants = [new(a, [new(31, 480, 1020)], null, null, false, null), new(b, null, null, null, true, null)];

        var first = Compile(credentials, grants);
        var reversed = Compile(credentials.Reverse().ToList(), grants.Reverse().ToList());
        var changed = Compile(credentials, grants.Take(1).ToList());

        Assert.Equal(first.Hash, reversed.Hash);
        Assert.Equal(first, reversed with { Offsets = first.Offsets, Windows = first.Windows, Credentials = first.Credentials, Entries = first.Entries });
        Assert.NotEqual(first.Hash, changed.Hash);
        Assert.Equal(64, first.Hash!.Length);
        Assert.Equal(first.Hash, PolicyCompiler.HashOf(first));
    }

    [Fact]
    public void A_compiled_policy_decides_like_the_algorithm()
    {
        var subject = Guid.NewGuid();
        var policy = Compile([new(subject, "card", null, null)], [new(subject, [new(31, 480, 1020)], null, null, false, null)]);
        var tuesday10Local = new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);

        Assert.True(AccessDecision.Evaluate(policy, "card", tuesday10Local).Allowed);
        Assert.Equal(AccessReasonNames.NoPermission, AccessDecision.Evaluate(policy, "card", tuesday10Local.AddHours(8)).Reason);
        Assert.Equal(AccessReasonNames.CredentialInvalid, AccessDecision.Evaluate(policy, "otra", tuesday10Local).Reason);
    }

    // --- Desplazamientos ---

    [Fact]
    public void A_time_zone_without_daylight_saving_has_a_single_offset()
    {
        var offsets = AccessPolicyService.Offsets("America/Argentina/Cordoba", Now);

        Assert.Equal([new PolicyOffset(null, -10800)], offsets);
        Assert.Equal(offsets, AccessPolicyService.Offsets("America/Argentina/Cordoba", Now.AddDays(30)));
    }

    [Fact]
    public void A_time_zone_with_daylight_saving_lists_its_transitions()
    {
        var offsets = AccessPolicyService.Offsets("Europe/Madrid", Now);

        Assert.Equal(new PolicyOffset(null, 3600), offsets[0]);
        Assert.Contains(new PolicyOffset(new DateTimeOffset(2027, 3, 28, 1, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), 7200), offsets);
        Assert.Contains(new PolicyOffset(new DateTimeOffset(2027, 10, 31, 1, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), 3600), offsets);
    }
}
