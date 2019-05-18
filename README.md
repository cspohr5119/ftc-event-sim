# FTC EventSim
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
Detroit World Championship Edison Division, the url is https://theorangealliance.org/events/1819-CMP-DET1 
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
  Title="My Tournament"    // Title message added to beginning of output
  EventKey="1819-CMP-DET"  // The event key from theorangealliance.org, found in the event URL.
  TeamPPMFile=""		   // Relative path to a file of TeamNumber,PPM (points per match) to load instead of EventKey.
  DataFilesFolder="DataFiles"  // Relative path to a folder containing download files from TOA.
  SchedulingModel="SwissScheduling"  // Supported values are "SwissScheduling", "RandomScheduling"
  TBPMethod="Expression"   // Supported values are "LosingScore", "WinningScore", "OwnScore", "TotalScore", "Expression"
  TBPExpresson="[OwnScore] + [LosingScore]" // Custom expression to calculate TBP (if TBPMethod is "Expression")
  ScoreRandomness="0.1"    // Value beteen 0 and 1 and will make score = PPM +/- (PPM * Rnd * ScoreRandomness)
  RandomTightness="2.0"    // Tightness of the peak of the Laplace distribution
  RandomSkew="0.8"         // Amount to skew the data.  <1.0 provides longer tails on the left of the distribution.
  Rounds="5"               // How many times each team will play in the tournament.
  Trials="1"               // Will run the same simulation n-times and will aggregate the stats
  OPRExcludesPenaltyPoints="false"  // If true, will deduct penalty points for OPR calculation
  OPRmmse="0"              // Minimum Mean Square Error: 1 - 3 recommended, 0 for traditional OPR values.
    
  RandomScheduling 
    UseFTCSchedule="true"   // If true, schedule the matches as they actually happened 
    UseFTCResults="false"  // If true, use actual match scores, otherwise, based on OPR 

  SwissScheduling 
    SeedFirstRoundsOPR="false"    // Use initial PPM (set to initial OPR) to seed first rounds as Swiss (Random if false).
    RoundsToScheduleAtStart="1"   // How many rounds to schedule at the start of the day
    ScheduleRoundsAhead="2"       // How many rounds ahead to schedule.  To schedule round 3 when round 1 is finishes, set to 2.
    ScheduleAtBreaks="false"      // Generate a schedule of n matches at the start of each day where n = RoundsToScheduleAtStart
    BreaksAfter="2,7"             // When the overnight breaks are. Could be used in one-day events as intermissions
    OpponentPairingMethod="Fold"  // Method to find ideal opponents: "Fold" or "Slide"
    AlliancePairingMethod="Slide" // Method to find ideal partners: "Fold" or "Slide"
    StartingRoundsOpponentPairingMethod = "Slide" // Method to pair seeded opponents at start of tournament
    CostForPreviousOppoent="100"  // Cost penalty for same opponent twice
    CostForPreviousAlignment="10" // Cost penalty for same alliance twice 
    CostForCrossingGroups="10"    // Cost penalty for opponents in different RP groups

  Output 
    Status="true"                   // Messages that say what's currently happening
    Headings="true"                 // Table header rows
    Matchups="true"                 // Show teams' matchups for each round
    IncludeCurrentRank="true"       // Not yet supported
    RankingsAfterEachRound="false"  // Show ranking table after each round
    FinalRankings="true"            // Ranking table at end of event
    TopXStats="6"                   // Number of teams in TopX stats.
    TrialStats="true"               // Stats for each trial
    BatchStats="true"               // Aggregated stats (sums and averages across multiple trials in a batch)
    Title="true"                    // Show Title value from the optons at start of run
~~~~

## Simulated Rankings
The following is a partial listing of a ranking report the simulator generates at the end of the run.
See the Stats section below for some definitions.

~~~~
Rank    Number  PPM     RP      TBP     OPR     OPRRank OPRDif  PPMRank PPMDif
1       6929    317.4   16      8083    317.4   1       0       1       0
2       10337   315.7   14      8297    315.7   2       0       2       0
3       9441    297.4   14      8183    297.6   3       0       3       0
4       11089   287.0   14      7626    286.9   4       0       4       0
5       12670   282.8   14      7384    282.7   5       0       5       0
6       10641   261.7   14      7182    261.5   9       3       9       3
7       12808   265.2   14      6780    265.3   8       1       8       1
8       5214    245.3   12      6812    245.3   10      2       10      2
9       5667    245.1   12      6660    245.3   11      2       11      2
10      5220    232.8   12      6512    232.7   13      3       13      3
...
~~~~

## Stats
The ranking table includes each team's calculated OPR values as calculated from simulated match scores.  
Downloaded OPR values are used at the beginning to set PPM, which is the base of points a team scores
in simulated matches.

Sample output:
~~~~
Teams   Matches High    Low     Avg     OPRDif  TopX    TopXDif OPRTopX PPMTopX
64      144     583     87      536.47  5.22    7       0.57    5       5
~~~~

**Teams** is the number of teams in the event.

**Matches** is the number of all matches in the event.

**High** is the highest alliance score.

**Low** is the lowest alliance score.

**Avg** is the average alliance score.

**OPRDif** is the average difference between a team's final rank and their rank if ordered by OPR.

**TopX** is the TopX value specified in the options, which affects the following values...

**TopXDiff** is the average difference between the top teams's final rank and OPR rank.

**OPRTopX** is the number of top teams in the top ranks.

**PPMTopX** is the number of top teams by PPM in the top ranks.

Diffs are how many places "off" the actual rank is. -8 means a team ended with a rank 8 places
below (or worse than) where they would have been if ranked by OPR.

**OPR** and **PPM** are similar, but mean different things.  PPM (points per match) is the number used by the
simulator to generate scores with random variations as specified in the options.  OPR is calculated from match 
results, estimating teams' average points per match from alliance totals.  These values should be close, and 
in fact, virtually equal if there is no randomness applied.

## Change Log
5/18/2019 Added support for Laplace distribution in ScoreRandomness.  Studing real event data, the random distribution
seemed to follow this type of curve.  Tighness and skew are also controlled by values in the options file.

5/14/2019 Added multi-day tournaments with RoundsToScheduleAtStart, ScheduleAtBreaks, BeaksAfter, ScheduleRoundsAhead,
and StartingRoundsOpponentPairingMethod options.

5/12/2019 Added first-round seeding for Swiss Tournaments.  Seed by rank based on PPM.

5/11/2019 Renamed Team.OPR to Team.PPM (points per match), which is a static value set at the start of the event.

5/11/2018 Calculating OPR on the fly from match scores, stored in Team.CurrentOPR.

5/11/2019 Added support for TeamPPMFile, which will load an arbitrary list of team numbers with PPM values.

5/11/2019 Fixed spelling in some Scheduling option names.

5/11/2019 Miscellaneous refactoring.

5/10/2019 Added expression evaluator for TBP.

5/9/2019 Added OPR calculation options: OPRExcludesPenaltyPoints and OPRmmse.

5/9/2019 Added options for Title and whether or not to output the title.

5/9/2019 Added ability to override one or more options from the command line.  For example, 
    
    EventSim.exe Options\myoptions.xml Title="My Tournament" Trials=100 Output.Headers=false
 