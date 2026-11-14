# Slot Game

A three-reel slot machine built in Unity using the assets supplied for the assignment.

**[Play in your browser](https://viditm.itch.io/slot-game)**

## How to Play

Open the itch.io link on a desktop browser

- Start with 1,000 credits.
- Use the arrows beside the bet amount to choose between 10, 25, and 50 credits.
- Click **SPIN** to play. The bet is deducted when the spin starts.
- Match all three symbols on the middle row to win.
- Use **New Session** to reset your credits after confirming.

Controls are locked during a spin. If the selected bet is too expensive, choose a smaller one. When credits fall below the minimum bet, the game offers a new session.


## Payouts

Winning payouts are added after all three reels stop.

| Symbol | Payout |
| --- | --- |
| Seven | Bet × 30 |
| Cherries | Bet × 5 |
| Bell | Bet × 10 |
| Bar | Bet × 15 |

For example, matching three Bells with a bet of 10 awards 100 credits. Only the middle row counts toward a win.

## Approach

The game rules are separate from the UI and reel animation. `SlotSession` handles credits, bets, and spin state. `SlotOutcomeGenerator` uses `System.Random` with configurable symbol weights to select each reel independently. All four symbols currently have equal weights.

Outcomes are selected before the animation begins. The reels animate toward those results and stop from left to right. Payouts are applied once, after the final reel stops.

Each reel reuses five symbol images inside a masked viewport. This allows continuous scrolling without creating new objects during each spin.

Starting credits, bet options, symbol weights, and payout multipliers are configured through the `SlotGameConfig` ScriptableObject.

## Open in Unity

Built with **Unity 6000.3.27f2**.

1. Clone or download this repository.
2. Add the project folder through Unity Hub and open it.
3. Open `Assets/_SlotGame/Scenes/SlotGame.unity`.
4. Enter Play Mode.

The exported WebGL build is included in `Build/WebGL`.

## Tests

The project includes 10 EditMode tests covering outcome generation and session rules, including payouts, affordability, duplicate spin requests, and session resets.

Run them through **Window → General → Test Runner → EditMode → Run All**.

## Scope and Assets

The project focuses on the core slot-machine requirements. No optional bonus rounds were added.

The slot-machine artwork and UI sprites were supplied with the assignment.