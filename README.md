# ftc-event-sim
A simulator to experiment with different FTC scheduling and ranking methods.

Author: Chuck Spohr, Team 5119 Baryons Mentor<br/>
Contact: chuckspohr@gmail.com

This project is a C# .NET console app developed in Visual Studio 2019 Community Edition and 
requires the Microsoft .NET framework 4.7.2.

## Summary
This is an FTC event simulator.  It will "re-run" a past FTC tournament's qualifying rounds
under different scheduling systems and Tie-Breaker Point (TBP) rules that you configure.
It reads team and match data from theorangealliance.org, then replays the tournament 
(qualifying rounds only) per your specification.

Want to base TBP on the winning alliance score?  No problem.  How about the total score?
You can do that too.

If you think random scheduling really wrecks RP for a lot of teams (it does), how can you
prove it?  Run the actual tournament with actual scores, then compare it to the same
tournament scheduled by the [Swiss tournament system](https://en.wikipedia.org/wiki/Swiss-system_tournament).
Check out the difference.

How do you see if your new system improves things?  It's easy.  Final rankings are shown
along with OPR rank.  I think most people would agree that the top teams in OPR should
be the top teams in rank if the scheduling and ranking systems are working well.  The 
statistics will give you the variance from OPR rank.  In other words, it the #1 OPR team 
ranks 10th, the stats will show a -9 variance for that team.  The average absolute value 
of variance is also given, which indicates the average difference between OPR Rank and FTC rank.
Let's see how low we can get THAT value.

## Running EventSim.exe
If you simply wish to run the binaries, copy the files from the **EventSim\bin\debug** folder 
to a more convenient place, and run from the command line:

    EventSim.exe [optionsFile] 

optionsFile is the relative path to an XML options file.  Some useful examples are in the Options 
folder.

A file of matches is automatically downloaded and saved locally when you provide an event
key from theorangealliance.org.  To find an event key, visit https://theorangealliance.org and
navigate to the event of interest.  The event key is in the url.  For examle, navigating to the
Missouri State Championship, T Division, the url is https://theorangealliance.org/events/1819-CMP-DET1 
so the event key is 1819-CMP-DET1.

I recommend making copies of the example options files for experimentation.  The examples will run
known good configurations and are helpful for reference.

Note: If you are creating and testing new options files in the Visual Studio debugger, set the 
**Copy To Output Directory** property to **Copy always** or **Copy if newer** so that it will deploy
with the compiled code to the bin\debug folder.

    EventSim.exe Options\DetroitEdisonActual19.xml
will run the 2019 Detroit Edison qualifying rounds as they actually happened.  Matches will be 
scheduled and run with scores found in the files provided.  This is helpful to validate the 
simulator is producing correct results.  What's also fun about this is that you can change how
TBP is calculated and see what happens to the rankings as a result for the same matchups and scores.

    EventSim.exe Options\DetroitEdisonSwiss19.xml
will run the tournament with the Swiss scheduing model. The tournament is played in rounds where
the first round is scheduled randomly and subsequent rounds are scheduled by an algorithm designed
to match teams in a way to accurately sort out the ranking more accurately.
See https://en.wikipedia.org/wiki/Swiss-system_tournament for more information.

    EventSim.exe Options\DetroitEdisonRandom19.xml
will run the tournament with random scheduling using the same teams from an event, but randomly
scheduled similar to FTC's method.  The teams are the same, the schedule is different.  Matches
results are determined by adding alliance partners' OPR values.  This is helpful to see how
much a tournament ranking can vary based on schedule alone.

## What you get in the output
EventSim writes all output to the console.  This can be redirected to a file if you wish to save
the results for further analysis.  For example,

    EventSim.exe Options/Random.xml > randomresults.txt

Table-based data is tab-delimited.  There are many different outputs, any of which can be suppressed
in the options file.  See the Options section below for details.

## Limitations
For Swiss scheduling, make sure the number of teams is a multiple of 4.  I have not tested with odd
numbers.  If it produces results, don't trust them!

I have also not tested with surrogates in any event.  Random and Swiss scheduling may both be
askew if there is an odd number of team or surrogates in the match data.

I have ideas for modified Swiss schedules that are not yet implemented.  They're to address the
concern that Swiss scheduling requires a break at the end of each round to make the schedule for the
next.  One idea would involve scheduling two random rounds at the start, and scheduling round 3 
based on round 1 results.  Once round 2 is complete, schedule round 4.  This way, there are no pauses
between rounds.

Also, multi-day tournaments such as Worlds, have natural breaks, so this could be used to schedule Swiss 
rounds as well.  Perhaps schedule the two rounds for day 1 randomly, then for day 2, schedule rounds 
3 and 4 up front by Swiss, then round 5 scheduled once round 3 completes, etc.  Day 3 matches would 
work the same. None of this is implemented, but I've created options for them to support it when 
I get to it.

## Options
~~~~
  EventKey="1819-CMP-DET"  // The event key from theorangealliance.org, found in the event URL.
  SchedulingModel="SwissScheduling"  // Supported values are "SwissScheduling", "RandomScheduling"
  TBPMethod="LosingScore"  // Supported values are "LosingScore", "WinningScore", "OwnScore", "TotalScore"
  ScoreRandomness="0.1"    // Value beteen 0 and 1 and will make score = OPR +/- (OPR * ScoreRandomness)
  Rounds="5"               // How many times each team will play in the tournament.
  Trials="1"               // Will run the same simulation n-times and will aggregate the stats
  
  RandomScheduling 
    UseFTCSchdule="true"   // If true, schedule the matches as they actually happened 
    UseFTCResults="false"  // If true, use actual match scores, otherwise, based on OPR 

  SwissScheduling 
    SeedFirstRoundsOPR="false"    // Not yet suppored
    RoundsToScheduleAtStart="1"   // How many rounds to schedule randomly to start the tournament
    SchduleAtBreaks="false"       // Not yet supported 
    BreaksAfter="2,7"             // Not yet suppored
    OpponentPairingMethod="Fold"  // Method to find ideal opponents: "Fold" or "Slide"
    AlliancePairingMethod="Slide" // Method to find ideal partners: "Fold" or "Slide"
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
The ranking table includes each team's OPR values as calculated from actual match scores download
from TOA.  OPR is used to caclulate scores for theoretical matchups.

OPRRank is also shown for each team in the rankings. This is what the team's ranking would be if
ranking were solely on OPR.  

Variance is how many places "off" the actual rank is. -8 means a team ended with a rank 8 places
below (or worse than) where they would have been if ranked by OPR.

The stats section includes:

AvgVar is the average absolute value of Variance for that event.  Lower numbers are obviously
better, indicating teams where generally where they should be in the rankings.

TopXVar is like AvgVariance, but for just the TopX number of teams.  This is useful to see if the
"right" teams are ending up as alliance captains.

InTopX is the number of TopX teams in OPR who made it into the TopXRank.  For example,
how many of the top 6 OPR teams made it into the Top 6 ranks?
