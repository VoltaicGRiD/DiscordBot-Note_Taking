# Note-Taking Discord Bot
Named "Imagination Creations" after a buddy of mine's TTRPG campaigning service. This discord bot is utilized for note-taking and processing via commands run through Discord Text Chat.

## WORK IN PROGRESS
The following tasks must be done
[] Commenting code
[] Updating infrastrucutre to improve MySQL performance
[] Additional commands to allow GM to more easily manipulate games and game sessions
[] Implementation of voice-activated commands (if possible in current situation) to allow users to take notes in a voice channel without needing to type out commands

## Usage
Utilizing the Discord API, this bot responds to incoming commands ran in text chat to enable users to create and log notes for TTRPG sessions. These notes are stored in a local MySQL database. 

There are 2 types of notes:
- Journal 
  a. Journal notes are global. As in, they persist across sessions, games, campaigns and more
- Note
  a. A note pertains to a particular game session, which must be created by the GM by using /session create
  
## Commands
`/help` creates a response outlining all commands available in the integration
`/session [sub-command]` all commands associated with note-taking / game session management
`/note [sub-command]` all commands associated with adding a 'note'. Players must be added to a session before /note will work
`/journal [sub-command]` all commands associated with creating journal entries.

