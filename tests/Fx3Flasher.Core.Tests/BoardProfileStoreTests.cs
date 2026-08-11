using Fx3Flasher.Core.Profiles;
using Xunit;

namespace Fx3Flasher.Core.Tests
{
    public class BoardProfileStoreTests
    {
        private const string TwoBoards = @"[
          { 'name': 'BoardA', 'bootloaderIds': [ { 'vendorId': 1204, 'productId': 243 } ] },
          { 'name': 'BoardB', 'applicationIds': [ { 'vendorId': 1204, 'productId': 244 } ] }
        ]";

        private static string Json(string s)
        {
            return s.Replace('\'', '"');
        }

        [Fact]
        public void Resolve_ReturnsMatchingProfile()
        {
            var store = new BoardProfileStore();
            store.LoadFromJson(Json(TwoBoards));

            bool ambiguous;
            var profile = store.Resolve(1204, 243, out ambiguous);

            Assert.NotNull(profile);
            Assert.Equal("BoardA", profile.Name);
            Assert.False(ambiguous);
        }

        [Fact]
        public void Resolve_ReturnsNull_WhenNoMatch()
        {
            var store = new BoardProfileStore();
            store.LoadFromJson(Json(TwoBoards));

            bool ambiguous;
            var profile = store.Resolve(0x1234, 0x5678, out ambiguous);

            Assert.Null(profile);
            Assert.False(ambiguous);
        }

        [Fact]
        public void Resolve_FlagsAmbiguous_WhenMultipleProfilesMatch()
        {
            const string overlapping = @"[
              { 'name': 'BoardA', 'bootloaderIds': [ { 'vendorId': 1204, 'productId': 243 } ] },
              { 'name': 'BoardDup', 'bootloaderIds': [ { 'vendorId': 1204, 'productId': 243 } ] }
            ]";

            var store = new BoardProfileStore();
            store.LoadFromJson(Json(overlapping));

            bool ambiguous;
            var profile = store.Resolve(1204, 243, out ambiguous);

            Assert.Null(profile);
            Assert.True(ambiguous);
        }
    }
}
