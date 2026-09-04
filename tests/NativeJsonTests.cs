namespace lancedb.tests
{
    using System.Text.Json;

    public class NativeJsonTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("sourceId")]
        [InlineData("Русский 中文 \"quoted\"\0value")]
        public void JsonBufferContainsExactPayloadAndTerminator(string value)
        {
            var payload = new[] { value };
            byte[] expected = JsonSerializer.SerializeToUtf8Bytes(payload);
            byte[] actual = NativeCall.ToJsonUtf8(payload);
            Assert.Equal(expected.Length + 1, actual.Length);
            Assert.Equal(expected, actual[..^1]);
            Assert.Equal(0, actual[^1]);
            Assert.DoesNotContain((byte)0, actual[..^1]);
        }

        [Fact]
        public void EmptyAndNullJsonAreTerminated()
        {
            Assert.Equal("[]\0"u8.ToArray(), NativeCall.ToJsonUtf8(Array.Empty<string>()));
            Assert.Equal("{}\0"u8.ToArray(), NativeCall.ToJsonUtf8(new Dictionary<string, string>()));
            Assert.Equal("null\0"u8.ToArray(), NativeCall.ToJsonUtf8<string?>(null));
        }

        [Fact]
        public void EveryIndexConfigurationIsTerminatedJson()
        {
            Index[] indexes = [new BTreeIndex(), new BitmapIndex(), new LabelListIndex(),
                new FmIndex(), new FtsIndex(), new IvfPqIndex(), new HnswPqIndex(),
                new IvfFlatIndex(), new IvfSqIndex(), new IvfRqIndex(),
                new HnswSqIndex(), new HnswFlatIndex()];
            foreach (Index index in indexes)
            {
                byte[] bytes = index.ToConfigJsonUtf8();
                Assert.Equal(0, bytes[^1]);
                using var json = JsonDocument.Parse(bytes.AsMemory(0, bytes.Length - 1));
                Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
            }
        }

        [Fact]
        public async Task RepeatedMergeInsertAndQueryPreserveJsonAcrossNativeBoundary()
        {
            using var batch = TestHelpers.CreateTestBatch(3);
            using var fixture = await TestFixture.CreateWithTable("json_boundary", batch);
            for (int i = 0; i < 32; i++)
            {
                await fixture.Table.MergeInsert("id").WhenMatchedUpdateAll()
                    .WhenNotMatchedInsertAll().Execute(batch);
                using var query = fixture.Table.Query();
                byte[] bytes = query.SerializeParamsUtf8();
                Assert.Equal(0, bytes[^1]);
                using var result = await query.ToArrow();
                Assert.Equal(3, result.Length);
            }
        }
    }
}
