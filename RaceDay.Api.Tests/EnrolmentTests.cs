using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class EnrolmentTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public EnrolmentTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(HttpClient organiser, EventDto ev, CategoryDto category)> SetUpEventAsync(int maxParticipants = 100)
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(organiser, "10K Race");
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId, "Senior", maxParticipants);
            return (organiser, ev, category);
        }

        // ------------------------------------------------------------- entering

        [Fact]
        public async Task Enrol_AsParticipant_ReturnsCreatedAndOrganiserCanSeeIt()
        {
            var (organiser, ev, category) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var response = await participant.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = category.CategoryId });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
            Assert.Equal(ev.EventId, enrolment!.EventId);
            Assert.Equal(category.CategoryId, enrolment.CategoryId);
            Assert.Equal("Pending", enrolment.Status);

            var organiserView = await organiser.GetAsync($"/api/enrolments/event/{ev.EventId}");
            Assert.Equal(HttpStatusCode.OK, organiserView.StatusCode);
            var enrolments = await organiserView.Content.ReadFromJsonAsync<List<EnrolmentDto>>();
            Assert.Single(enrolments!);
            Assert.Equal(enrolment.EnrolmentId, enrolments![0].EnrolmentId);
        }

        [Fact]
        public async Task Enrol_TwiceInSameCategory_ReturnsBadRequest()
        {
            var (_, ev, category) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);
            var second = await participant.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = category.CategoryId });

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Enrol_InTwoCategoriesOfTheSameEvent_BothSucceed()
        {
            var (organiser, ev, firstCategory) = await SetUpEventAsync();
            var secondCategory = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId, "Masters");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var first = await participant.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = firstCategory.CategoryId });
            var second = await participant.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = secondCategory.CategoryId });

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        }

        [Fact]
        public async Task Enrol_AsOrganiser_ReturnsForbidden()
        {
            var (organiser, ev, category) = await SetUpEventAsync();

            var response = await organiser.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = category.CategoryId });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_WithoutLogin_ReturnsUnauthorized()
        {
            var (_, ev, category) = await SetUpEventAsync();
            var anonymous = _factory.CreateSessionClient();

            var response = await anonymous.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = category.CategoryId });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_WithCategoryFromAnotherEvent_ReturnsBadRequest()
        {
            var (_, ev, _) = await SetUpEventAsync();
            var (_, _, otherEventsCategory) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var response = await participant.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = otherEventsCategory.CategoryId });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_InMissingEvent_ReturnsNotFound()
        {
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var response = await participant.PostAsJsonAsync("/api/enrolments", new { eventId = 999999, categoryId = 1 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_WhenCategoryIsFull_ReturnsBadRequest()
        {
            var (_, ev, category) = await SetUpEventAsync(maxParticipants: 1);
            var first = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var second = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            await TestHelpers.EnrolAsync(first, ev.EventId, category.CategoryId);
            var response = await second.PostAsJsonAsync("/api/enrolments",
                new { eventId = ev.EventId, categoryId = category.CategoryId });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // --------------------------------------------------------------- viewing

        [Fact]
        public async Task GetEventEnrolments_ByNonOwningOrganiser_ReturnsForbidden()
        {
            var (_, ev, _) = await SetUpEventAsync();
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var response = await other.GetAsync($"/api/enrolments/event/{ev.EventId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetEventEnrolments_AsParticipant_ReturnsForbidden()
        {
            var (_, ev, _) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var response = await participant.GetAsync($"/api/enrolments/event/{ev.EventId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetMyEnrolments_AsParticipant_ReturnsOwnEnrolmentsOnly()
        {
            var (_, ev, category) = await SetUpEventAsync();
            var me = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var someoneElse = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            await TestHelpers.EnrolAsync(me, ev.EventId, category.CategoryId);
            await TestHelpers.EnrolAsync(someoneElse, ev.EventId, category.CategoryId);

            var response = await me.GetAsync("/api/enrolments/mine");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var enrolments = await response.Content.ReadFromJsonAsync<List<EnrolmentDto>>();
            Assert.Single(enrolments!);
        }

        [Fact]
        public async Task GetMyEnrolments_AsOrganiser_ReturnsForbidden()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.GetAsync("/api/enrolments/mine");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // ---------------------------------------------------------------- status

        [Fact]
        public async Task UpdateStatus_ByOwningOrganiser_ChangesTheStatus()
        {
            var (organiser, ev, category) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var enrolment = await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
                new { status = "Confirmed" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
            Assert.Equal("Confirmed", updated!.Status);

            var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/mine");
            Assert.Equal("Confirmed", mine![0].Status);
        }

        [Fact]
        public async Task UpdateStatus_WithInvalidStatus_ReturnsBadRequest()
        {
            var (organiser, ev, category) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var enrolment = await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
                new { status = "Banana" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateStatus_ByNonOwningOrganiser_ReturnsForbidden()
        {
            var (_, ev, category) = await SetUpEventAsync();
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var enrolment = await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var response = await other.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
                new { status = "Cancelled" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateStatus_AsParticipant_ReturnsForbidden()
        {
            var (_, ev, category) = await SetUpEventAsync();
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var enrolment = await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var response = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
                new { status = "Confirmed" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateStatus_ForMissingEnrolment_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var response = await organiser.PutAsJsonAsync("/api/enrolments/999999/status", new { status = "Confirmed" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
