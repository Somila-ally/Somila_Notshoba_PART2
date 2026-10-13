using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class EventTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public EventTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // ---------------------------------------------------------------- viewing

        [Fact]
        public async Task GetEvents_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();
            var response = await client.GetAsync("/api/events");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("Participant")]
        [InlineData("Organiser")]
        public async Task GetEvents_AsEitherRole_ReturnsOk(string role)
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, role);
            var response = await client.GetAsync("/api/events");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var response = await client.GetAsync("/api/events/999999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // --------------------------------------------------------------- creating

        [Fact]
        public async Task CreateEvent_AsOrganiser_ReturnsCreatedWithAllCapturedFields()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventPayload("Bay Run", 21.1m, "Run"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<EventDto>();
            Assert.Equal("Bay Run", created!.Name);
            Assert.Equal(21.1m, created.DistanceKm);
            Assert.Equal("Run", created.EventType);
            Assert.False(string.IsNullOrWhiteSpace(created.Location));
            Assert.False(string.IsNullOrWhiteSpace(created.Description));
        }

        [Fact]
        public async Task CreateEvent_AsParticipant_ReturnsForbidden()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventPayload());
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();
            var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventPayload());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventPayload(eventType: "Swim"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_WithMissingName_ReturnsBadRequest()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var response = await client.PostAsJsonAsync("/api/events", new
            {
                name = "",
                date = DateTime.UtcNow.AddDays(5),
                location = "PE",
                distanceKm = 5m,
                eventType = "Run"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_WithZeroDistance_ReturnsBadRequest()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventPayload(distanceKm: 0m));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ------------------------------------------------------------------ mine

        [Fact]
        public async Task GetMyEvents_AsOrganiser_ReturnsOnlyOwnEvents()
        {
            var organiserA = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var organiserB = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var mine = await TestHelpers.CreateEventAsync(organiserA, "A's Event");
            await TestHelpers.CreateEventAsync(organiserB, "B's Event");

            var response = await organiserA.GetAsync("/api/events/mine");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
            Assert.Single(events!);
            Assert.Equal(mine.EventId, events![0].EventId);
        }

        [Fact]
        public async Task GetMyEvents_AsParticipant_ReturnsForbidden()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var response = await client.GetAsync("/api/events/mine");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // ------------------------------------------------------ update / delete

        [Fact]
        public async Task UpdateEvent_ByOwner_ReturnsUpdatedEvent()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner, "Original");

            var response = await owner.PutAsJsonAsync($"/api/events/{ev.EventId}",
                TestHelpers.EventPayload("Renamed", 5m, "Walk"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.Content.ReadFromJsonAsync<EventDto>();
            Assert.Equal("Renamed", updated!.Name);
            Assert.Equal("Walk", updated.EventType);
            Assert.Equal(5m, updated.DistanceKm);
        }

        [Fact]
        public async Task UpdateAndDeleteEvent_ByNonOwningOrganiser_ReturnsForbidden()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner, "Owner's Event");

            var update = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.EventPayload("Hijacked"));
            Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

            var delete = await other.DeleteAsync($"/api/events/{ev.EventId}");
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        }

        [Fact]
        public async Task UpdateAndDeleteEvent_AsParticipant_ReturnsForbidden()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var ev = await TestHelpers.CreateEventAsync(owner);

            var update = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.EventPayload("Nope"));
            Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

            var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        }

        [Fact]
        public async Task UpdateEvent_ThatDoesNotExist_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.PutAsJsonAsync("/api/events/999999", TestHelpers.EventPayload());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteEvent_ByOwner_ReturnsOkAndEventIsGone()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner, "To Be Deleted");
            await TestHelpers.CreateCategoryAsync(owner, ev.EventId);

            var delete = await owner.DeleteAsync($"/api/events/{ev.EventId}");
            Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

            var get = await owner.GetAsync($"/api/events/{ev.EventId}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        }

        [Fact]
        public async Task DeleteEvent_WithEnrolments_ReturnsBadRequest()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.EventId);
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var delete = await owner.DeleteAsync($"/api/events/{ev.EventId}");

            Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        }
    }
}
