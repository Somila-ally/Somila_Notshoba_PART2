using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class ResultTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ResultTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(HttpClient organiser, HttpClient participant, EventDto ev, EnrolmentDto enrolment)> SetUpEnrolledParticipantAsync()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(organiser, "5K Time Trial");
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId, "Open");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var enrolment = await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);
            return (organiser, participant, ev, enrolment);
        }

        private static object ResultPayload(int enrolmentId, string finishTime = "00:25:30", int position = 1) => new
        {
            enrolmentId,
            finishTime,
            finishPosition = position
        };

        // -------------------------------------------------------------- recording

        [Fact]
        public async Task CreateResult_ByOwningOrganiser_ReturnsCreated()
        {
            var (organiser, _, _, enrolment) = await SetUpEnrolledParticipantAsync();

            var response = await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ResultDto>();
            Assert.Equal("00:25:30", result!.FinishTime);
            Assert.Equal(1, result.FinishPosition);
            Assert.Equal(enrolment.EnrolmentId, result.EnrolmentId);
        }

        [Fact]
        public async Task CreateResult_ByNonOwningOrganiser_ReturnsForbidden()
        {
            var (_, _, _, enrolment) = await SetUpEnrolledParticipantAsync();
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var response = await other.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateResult_AsParticipant_ReturnsForbidden()
        {
            var (_, participant, _, enrolment) = await SetUpEnrolledParticipantAsync();

            var response = await participant.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateResult_WithoutLogin_ReturnsUnauthorized()
        {
            var anonymous = _factory.CreateSessionClient();
            var response = await anonymous.PostAsJsonAsync("/api/results", ResultPayload(1));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateResult_ForMissingEnrolment_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.PostAsJsonAsync("/api/results", ResultPayload(999999));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateResult_Twice_ReturnsBadRequest()
        {
            var (organiser, _, _, enrolment) = await SetUpEnrolledParticipantAsync();

            await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));
            var second = await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId, "00:20:00"));

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Theory]
        [InlineData("not-a-time")]
        [InlineData("00:00:00")]
        public async Task CreateResult_WithInvalidFinishTime_ReturnsBadRequest(string finishTime)
        {
            var (organiser, _, _, enrolment) = await SetUpEnrolledParticipantAsync();

            var response = await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId, finishTime));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateResult_ForCancelledEnrolment_ReturnsBadRequest()
        {
            var (organiser, _, _, enrolment) = await SetUpEnrolledParticipantAsync();
            await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Cancelled" });

            var response = await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---------------------------------------------------------------- viewing

        [Fact]
        public async Task GetMyResults_AsParticipant_ReturnsOwnRecordedResult()
        {
            var (organiser, participant, ev, enrolment) = await SetUpEnrolledParticipantAsync();
            await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId, "00:22:15", 2));

            var response = await participant.GetAsync("/api/results/mine");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var results = await response.Content.ReadFromJsonAsync<List<ResultDto>>();
            Assert.Single(results!);
            Assert.Equal(2, results![0].FinishPosition);
            Assert.Equal(ev.EventId, results[0].EventId);
        }

        [Fact]
        public async Task GetMyResults_AsOrganiser_ReturnsForbidden()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.GetAsync("/api/results/mine");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetEventResults_ByOwner_ReturnsResults_ButNotForOtherOrganiserOrParticipant()
        {
            var (organiser, participant, ev, enrolment) = await SetUpEnrolledParticipantAsync();
            await organiser.PostAsJsonAsync("/api/results", ResultPayload(enrolment.EnrolmentId));
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");

            var ok = await organiser.GetAsync($"/api/results/event/{ev.EventId}");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var results = await ok.Content.ReadFromJsonAsync<List<ResultDto>>();
            Assert.Single(results!);

            var otherOrganiser = await other.GetAsync($"/api/results/event/{ev.EventId}");
            Assert.Equal(HttpStatusCode.Forbidden, otherOrganiser.StatusCode);

            var asParticipant = await participant.GetAsync($"/api/results/event/{ev.EventId}");
            Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        }

        [Fact]
        public async Task GetEventResults_ForMissingEvent_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.GetAsync("/api/results/event/999999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
