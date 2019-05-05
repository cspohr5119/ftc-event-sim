# ftc-event-sim
A simulator to experiment with different FTC scheduling and ranking methods

This project is a C# .NET console app developed in Visual Studio 2019 Community Edition.

If you simply wish to run the binaries, copy the files from the EventSim\bin\debug folder 
to a more convenient place, and run EventSim.exe [optionsFile] from the command line.

EventSim.exe takes one paramater: optionsFile.  

There are many options available to play with.  The options are stored in XML format in the same
folder as the .exe.  A few different options files are provided.

Schedules and Team OPR's are automatically downloadedc and saved locally when you provide an event
key from theorangealliance.org.  To find an event key, visit https://theorangealliance.org and
navigate to the event of interest.  The event key is in the url.  For examle, navigating to the
Missouri State Championship, T Division, the url is https://theorangealliance.org/events/1819-MO-cmp2 
so the event key is 1819-MO-cmp.

## Running EventSim.exe
I recommend making copies of the example options files for experimentation.  The examples will run
known good configurations and are helpful for reference.

EventSim.exe ActualResults.xml
will run the 2019 Detroit World Championship as it happened.  Matches will be scheduled and run
with scores found in the files provided.  This is helpful to validate the simulator is producing
correct results.  What's also fun about this is that you can change how TBP is calculated and see
what happens to the rankings as a result for the same matchups and scores.

EventSim.exe Swiss.xml
will run a tournament with the Swiss scheduing model. The tournament is played in rounds where
the first round is scheduled randomly and subsequent rounds are scheduled by an algorithm designed
to match teams in a way to accurately sort out the ranking more accurately.
See https://en.wikipedia.org/wiki/Swiss-system_tournament for more information.

EventSim.exe Random.xml
will run a tournament with random scheduling using the same teams from an event, but randomly
scheduled similar to FTC's method.  The teams are the same, the schedule is different.  Matches
results are determined by adding alliance partners' OPR values.  This is helpful to see how
much a tournament ranking can vary based on schedule alone.

## What you get in the output
EventSim writes all output to the console.  This can be redirected to a file if you wish to save
the results for further analysis.  Table-based data is tab-delimited.

EventSim.exe Random.xml > randomresults.txt

There are many different outputs, any of which can be suppressed in the options file.  See the
Options section below for details

## Options
~~~~
  EventFile="DataFiles\2019RollaT.csv"   
  OPRFile="DataFiles\2019RollaTopr.tab" 
  SchedulingModel="SwissScheduling"  // Supported values are "SwissScheduling", "RandomScheduling"
  TBPMethod="LosingScore"  // Supported values are "LosingScore", "WinningScore", "OwnScore", "TotalScore"
  ScoreRandomness="0.1"  // Value beteen 0 and 1 and will make score = OPR +/- (OPR * ScoreRandomness)
  Rounds="5" 
  Trials="1"  // Will run the same simulation n-times and will aggregate the stats
  
  RandomScheduling 
    UseFTCSchdule="true" // If true, schedule the matches as they actually happened 
    UseFTCResults="false"  // If true, use actual match scores, otherwise, based on OPR 

  SwissScheduling 
    SeedFirstRoundsOPR="false"    // Not yet suppored
    RoundsToScheduleAtStart="1"   // How many rounds to schedule randomly to start the tournament
    SchduleAtBreaks="false"       // Not yet supported 
    BreaksAfter="2,7"             // Not yet suppored
    OpponentPairingMethod="Fold"  // Method to find ideal opponents
    AllianceParingMethod="Slide"  // Method to find ideal partners
    CostForPreviousOppoent="100"  // Cost penalty for same opponent twice
    CostForPreviousAlignment="10" // Cost penalty for same alliance twice 
    CostForCrossingGroups="10"    // Cost penalty for opponents in different RP groups

  Output 
    Status="true"                   // Messages that say what's currently happening
    Headings="true"                 // Table header rows
    Matchups="true"                 // Show teams matchups for each round
    IncludeCurrentRank="true"       // Not yet supported
    RankingsAfterEachRound="false"  // Show ranking table after each round
    FinalRankings="true"            // Ranking table at end of event
    TopXStats="6"                   // Number of teams in TopX stats.
    TrialStats="true"               // Stats for each event
    BatchStats="true"               // Aggregated stats (for multiple trials in a batch)
~~~~

## Stats
The ranking table includes each team's OPR value as found on TOA.  OPR is not calculated on the fly.
It must be known up-front in order to caclulate scores for theoretical matchups.

OPRRank is also shown for each team in the rankings. This is what the team's ranking would be if
ranking were solely on OPR.  

Variance is how many places "off" the actual rank is. -2 means a team ended with a rank two places
below where they would have been if ranked by OPR.

The stats section includes...

AvgVariance is the average absolute value of Variance for that event.  Lower numbers are obviously
better, indicating teams where generally where they should be in the rankings.

TopXVar is like AvgVariance, but for just the TopX number of teams.  This is useful to see if the
"right" teams are ending up as alliance captains.

TopOprInTopRank is the number of TopX teams in OPR who made it into the TopXRank.  For example,
how many of the top 6 OPR teams made it into the Top 6 ranks?
