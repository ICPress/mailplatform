public class EmalQueueRow {
 public uint queue_id { get; set; }
 public string email { get; set; } = "";

 public EmailType type { get; set; }

 public string? additional_data { get; set; }

 public bool claimed { get; set; }

}
