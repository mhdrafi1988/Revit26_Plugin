namespace Revit26.RoofTagV42.Models
{
    public class TaggingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TagsPlaced { get; set; }
        public int TagsFailed { get; set; }
        public int TotalPoints { get; set; }

        public TaggingResult()
        {
            Success = false;
            Message = string.Empty;
        }

        public static TaggingResult SuccessResult(int placed, int failed, int total)
        {
            return new TaggingResult
            {
                Success = true,
                TagsPlaced = placed,
                TagsFailed = failed,
                TotalPoints = total,
                Message = $"Successfully placed {placed} of {total} tags"
            };
        }

        public static TaggingResult FailureResult(string message)
        {
            return new TaggingResult
            {
                Success = false,
                Message = message
            };
        }
    }
}