using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Options;

namespace GDWebapp.Data
{
  public class GridDraftDeckAPI
  {
    private readonly GoogleDocsInfo _googleDocsInfo;

    public GridDraftDeckAPI(IOptions<GoogleDocsInfo> googleDocsInfo)
    {
      _googleDocsInfo = googleDocsInfo.Value ?? throw new ArgumentException("no info supplied");
    }

    static string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };
    static string ApplicationName = "GDGUI";
    public Task<List<GridDraftDeckData>> GetCardData()
    {
      GoogleCredential credential;

      using (var stream =
          new FileStream(_googleDocsInfo.GoogleKeyJsonFile, FileMode.Open, FileAccess.Read))
      {
        // The file token.json stores the user's access and refresh tokens, and is created
        // automatically when the authorization flow completes for the first time.
        string credPath = "token.json";
        credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
        Console.WriteLine("Credential file saved to: " + credPath);
      }

      var service = new SheetsService(new BaseClientService.Initializer()
      {
        HttpClientInitializer = credential,
        ApplicationName = ApplicationName,
      });

      // Define request parameters.
      String spreadsheetId = _googleDocsInfo.SpreadSheetId;
      String range = "Sheet1!A2:D";
      SpreadsheetsResource.ValuesResource.GetRequest request =
              service.Spreadsheets.Values.Get(spreadsheetId, range);

      ValueRange response = request.Execute();
      IList<IList<Object>> values = response.Values;
      var result = new List<GridDraftDeckData>();
      if (values != null && values.Count > 0)
      {
        
        foreach (var row in values)
        {
          // Print columns A and E, which correspond to indices 0 and 4.
          result.Add(new GridDraftDeckData()
          {
            Name = row[0].ToString(),
            Description = row[1].ToString(),
            Weight = int.Parse(row[2].ToString())
          });
        }
      }

      return Task.FromResult(result);
    }
  }

  public class GridDraftDeckData
  {
    public string Name { get; set; }
    public string Description { get; set; }
    public int Weight { get; set; }
  }

  public class GridDraftDeckObject
  {

    private Stack<GridDraftDeckData> Deck;
    private Stack<GridDraftDeckData> Graveyard;
    public GridDraftDeckData CurrentCard { get; private set; }
    public GridDraftDeckObject(IList<GridDraftDeckData> cards)
    {
      Deck = new Stack<GridDraftDeckData>();
      Graveyard = new Stack<GridDraftDeckData>();
      Deck = ToDeck(cards);
      Shuffle();
    }

    public Stack<GridDraftDeckData> ToDeck(IList<GridDraftDeckData> cards)
    {
      var d = new Stack<GridDraftDeckData>();
      for(int i = cards.Count -1; i >= 0; i--)
      {
        d.Push(cards[i]);
      }
      return d;
    }

    public void Shuffle()
    {
      Random rng = new Random();
      var shuffleCounter = rng.Next(5, 12);
      int n = Deck.Count;
      var shuffleDeck = Deck.ToList();
      //shuffle between 5 and 12 times
      for(int i = 0; i < shuffleCounter; i++)
      {
        while (n > 1)
        {
          n--;
          int k = rng.Next(n + 1);
          GridDraftDeckData value = shuffleDeck[k];
          shuffleDeck[k] = shuffleDeck[n];
          shuffleDeck[n] = value;

        }
      }

      //after shuffling, check weights
      for(int i = 0; i< Deck.Count; i++)
      {
        var currentCard = shuffleDeck[i];
        var value = currentCard;

        //if card has no weight, move on
        if (currentCard.Weight == 0)
          continue;


        int swapCardIndex = i;
        GridDraftDeckData swapCard = value;

        while (swapCard.Weight < 0 && (i - Deck.Count - 1) >= swapCard.Weight)
        {
          swapCardIndex = rng.Next(0, Deck.Count + swapCard.Weight);
          swapCard = shuffleDeck[swapCardIndex];
        }

        while(swapCard.Weight > 0 && i < swapCard.Weight)
        {
          swapCardIndex = rng.Next(swapCard.Weight, Deck.Count);
          swapCard = shuffleDeck[swapCardIndex];
        }

        shuffleDeck[i] = shuffleDeck[swapCardIndex];
        shuffleDeck[swapCardIndex] = value;
      }

      Deck = ToDeck(shuffleDeck);
      CurrentCard = null;
    }

    public void DrawCard()
    {
      if(Deck.Count > 0) 
      {
        if(CurrentCard != null)
          Graveyard.Push(CurrentCard);
        CurrentCard = Deck.Pop();
      }
    }

    public int CardsRemaining() => Deck.Count;
    public int CardsDrawn() => Graveyard.Count + (CurrentCard != null ? 1 : 0);

    public void GetPreviousCard()
    {
      if(Graveyard.Count > 0)
      {
        if (CurrentCard != null)
          Deck.Push(CurrentCard);
        CurrentCard = Graveyard.Pop();
      }
    }
  }
}
