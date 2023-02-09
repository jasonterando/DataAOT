using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DataAOT.Test.TestClasses;

[Table( "user_accounts")]
public class UserAccountModel
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = default!;

    [Column("last_name")]
    public string LastName { get; set; } = default!;

    [Column("create_timestamp")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTimestamp { get; set; }

    [Column("update_timestamp")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTimestamp { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, typeof(UserAccountModel), SourceGenerationContext.Default);
    }
}
