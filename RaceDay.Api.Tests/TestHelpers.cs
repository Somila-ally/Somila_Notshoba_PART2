using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests
{
    /// <summary>Shared helpers so each test only spells out what it is actually testing.</summary>
    public static class TestHelpers
    {
        public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.com";

        /// <summary>Registers a new account, logs in, and returns a client that holds the session cookie.</summary>
        public static async Task<HttpClient> RegisterAndLoginAsync(
            CustomWebApplicationFactory factory, string role, string? email = null,
            string firstName = "Test", string lastName = "User")
        {
            var client = factory.CreateSessionClient();
            email ??= UniqueEmail(role.ToLowerInvariant());

            await client.PostAsJsonAsync("/api/auth/register", new
            {
                firstName,
                lastName,
                email,
                password = "Password1!",
                role
            });

            await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "Password1!"
            });

            return client;
        }

        public static object EventPayload(string name = "City Fun Run", decimal distanceKm = 10m, string eventType = "Run") => new
        {
            name,
            description = "A community race.",
            date = DateTime.UtcNow.AddDays(30),
            location = "Uitenhage",
            distanceKm,
            eventType
        };

        public static async Task<EventDto> CreateEventAsync(HttpClient organiser, string name = "City Fun Run")
        {
            var response = await organiser.PostAsJsonAsync("/api/events", EventPayload(name));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EventDto>())!;
        }

        public static async Task<CategoryDto> CreateCategoryAsync(
            HttpClient organiser, int eventId, string name = "Senior", int maxParticipants = 100)
        {
            var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
            {
                name,
                maxParticipants
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
        }

        public static async Task<EnrolmentDto> EnrolAsync(HttpClient participant, int eventId, int categoryId)
        {
            var response = await participant.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
        }

        public static async Task<ProfileDto> GetProfileAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/profile/me");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProfileDto>())!;
        }
    }
}
