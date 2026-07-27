using fNbt;
using HPSkyStatusUpdator.Models;
using System.IO.Compression;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class AuctionService
{
    private readonly HttpClient _client;

    private const string AuctionUrl =
        "https://api.hypixel.net/v2/skyblock/auctions";


    public AuctionService(HttpClient client)
    {
        _client = client;
    }


    public async Task<List<DecodedAuction>> GetAllAuctions()
    {
        var decodedAuctions = new List<DecodedAuction>();


        // Get first page to find total pages
        JsonDocument? firstPage =
            await GetPage(0);

        if (firstPage == null)
            return decodedAuctions;


        JsonElement root = firstPage.RootElement;


        if (!root.TryGetProperty(
                "totalPages",
                out var totalPagesElement))
        {
            return decodedAuctions;
        }


        int totalPages =
            totalPagesElement.GetInt32();

        Console.WriteLine(
            $"Downloading {totalPages} auction pages"
        );

        for (int page = 0; page <= totalPages; page++)
        {
            JsonDocument? document;


            if (page == 0)
            {
                document = firstPage;
            }
            else
            {
                document = await GetPage(page);
            }


            if (document == null)
                continue;


            if (!document.RootElement.TryGetProperty(
                    "auctions",
                    out var auctions))
            {
                continue;
            }


            foreach (var auction in auctions.EnumerateArray())
            {

                // Ignore anything without BIN
                if (!auction.TryGetProperty(
                        "bin",
                        out var bin))
                {
                    continue;
                }


                if (!bin.GetBoolean())
                    continue;






                long price =
                    auction.GetProperty(
                        "starting_bid")
                    .GetInt64();


                string? id = GetNbtId(auction);

                if (id == null)
                    continue;


                decodedAuctions.Add(new DecodedAuction
                {
                    ItemId = id,

                    Uuid =
                        auction.GetProperty("uuid")
                        .GetString() ?? "",

                    ItemName =
                        auction.GetProperty("item_name")
                        .GetString() ?? "",

                    Tier =
                        auction.GetProperty("tier")
                        .GetString() ?? "",

                    Price = price
                });

            }


            if (page % 5 == 0)
            {
                Console.WriteLine(
                    $"Checked page {page}/{totalPages}"
                );
            }
        }


        return decodedAuctions;
    }



    private async Task<JsonDocument?> GetPage(int page)
    {
        try
        {
            using var response =
                await _client.GetAsync(
                    $"{AuctionUrl}?page={page}"
                );


            if (!response.IsSuccessStatusCode)
                return null;


            string json =
                await response.Content.ReadAsStringAsync();


            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Auction page error {page}: {ex.Message}"
            );

            return null;
        }
    }



    



    private string? GetNbtId(JsonElement auction)
    {
        if (!auction.TryGetProperty("item_bytes", out var bytes))
        {

            return null;
        }

        string itemBytes = bytes.GetString()!;

        byte[] compressed = Convert.FromBase64String(itemBytes);

        using var compressedStream = new MemoryStream(compressed);
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);

        NbtFile file = new();

        file.LoadFromStream(
            gzip,
            NbtCompression.None);



        var root = file.RootTag;


        NbtCompound? item = null;

        foreach (NbtTag child in root.Tags)
        {
            if (child.Name == "i" && child is NbtList list)
            {
                item = list[0] as NbtCompound;
                break;
            }
        }


        if (item == null)
        {

            return null;
        }


        var tag = FindCompound(item, "tag");

        if (tag == null)
        {

            return null;
        }
        if (tag == null)
        {

            return null;
        }


        var extra = FindCompound(tag, "ExtraAttributes");
        if (extra == null)
        {

            return null;
        }




        if (extra == null)
            return null;


        // Check for pets
        var petInfo = FindString(extra, "petInfo");

        if (petInfo != null)
        {
            using JsonDocument petDoc = JsonDocument.Parse(petInfo);

            if (petDoc.RootElement.TryGetProperty(
                    "type",
                    out var type))
            {
                return type.GetString() + "_PET";
            }
        }


        // Normal items
        string? id = FindString(extra, "id");
      
        if (id == "PET")
        {
            string? petInfo2 = FindString(extra, "petInfo");

            if (petInfo2 != null)
            {
                using JsonDocument pet =
                    JsonDocument.Parse(petInfo2);

                if (pet.RootElement.TryGetProperty(
                        "type",
                        out var type))
                {
                    return type.GetString() + "_PET";
                }
            }
        }
        
       
        return id;
    }



    private NbtCompound? FindCompound(
        NbtCompound parent,
        string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name &&
                tag is NbtCompound compound)
            {
                return compound;
            }
        }

        return null;
    }



    private string? FindString(
        NbtCompound parent,
        string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name &&
                tag is NbtString str)
            {
                return str.Value;
            }
        }

        return null;
    }

     
}

// add searches for everything