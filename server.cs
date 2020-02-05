exec("./lib/jettison.cs");
exec("./lib/ascii.cs");

exec("./noOrb.cs");

exec("./board.cs");
exec("./music.cs");

$Server::PokemonAddress = "127.0.0.1:32145";
$Server::Pokemon::Connected = false;

$Server::Pokemon::FramesLeft = 10;

$Server::Pokemon::Palette = 0;

$Server::Pokemon::CurMusicBank = "0";
$Server::Pokemon::CurMusicTrack = "0";

$Server::Pokemon::InputMode = "A";

$Server::Pokemon::CenterCamPosition = "40.25 36.25 60 0.000915452 -0.706497 0.707715 3.13976";

function initPokemonConnection() {
	if(!isObject(PokemonTCPObject)) {
		new TCPObject(PokemonTCPObject);
	} else {
		PokemonTCPObject.disconnect();
	}

	%obj = PokemonTCPObject;
	%obj.connect($Server::PokemonAddress);

	if(!isObject(PokemonTCPLines)) {
		// blockland likes to merge multiple send commands being called at once into one line
		// "does this look stupid?" yes, but it's easy
		new GuiTextListCtrl(PokemonTCPLines);
	} else {
		PokemonTCPLines.clear();
	}

	if(!isObject(PokemonTCPToProcess)) {
		new GuiTextListCtrl(PokemonTCPToProcess);
	} else {
		PokemonTCPToProcess.clear();
	}
}
initPokemonConnection();

function PokemonTCPObject::onConnected(%this) {
	cancel($PokemonConnectRetryLoop);

	echo("Connected to the Pokemon server.");
	PokemonTCPObject.send("connect\r\n");

	$Server::Pokemon::Connected = true;

	if(!BrickGroup_888888.getCount()) {
		buildPokemonBoard();
	}
}

function PokemonTCPObject::onConnectFailed(%this) {
	cancel($PokemonConnectRetryLoop);
	echo("Trying to connect to the Pokemon server again (failed to connect)...");
	$Server::Pokemon::Connected = false;
	$Server::Pokemon::ProcessQueue = false;
	$PokemonConnectRetryLoop = %this.schedule(1000, connect, $Server::PokemonAddress);
}

function PokemonTCPObject::onDisconnect(%this) {
	cancel($PokemonConnectRetryLoop);
	echo("Trying to connect to the Pokemon server again (disconnected)...");
	$Server::Pokemon::Connected = false;
	$Server::Pokemon::ProcessQueue = false;
	$PokemonConnectRetryLoop = %this.schedule(1000, connect, $Server::PokemonAddress);
}

function fadeOutColorPKMNVI(%c, %s) {
	echo(%c);
	cancel($Server::Pokemon::ICSched);
	if(%s) {
		$Server::Pokemon::ICSched = schedule(500, 0, fadeOutColorPKMNVI, "ff7700", 0);
	}
	$Server::Pokemon::InputColor = %c;
}
function PokemonTCPObject::onLine(%this, %line) {
	%line = trim(%line);
	%cmd = getField(%line, 0);
	
	// this looks stupid but im trying to remain as optimized as possible

	if(%cmd $= "r") {
		$Server::Pokemon::ScreenData = getFields(%line, 1);
		renderScreen(0);
	}
	if(%cmd $= "v") {
		%whom = getField(%line, 1);
		%input = getField(%line, 2);

		if(isObject(%whom)) {
			if(%whom.getClassName() $= "GameConnection") {
				%whom.votedInput = %input;
			}
		}
	}
	if(%cmd $= "f") {
		$Server::Pokemon::FramesLeft = getField(%line, 1);
	}
	if(%cmd $= "i") {
		$Server::Pokemon::LastWinningInput = getField(%line, 1);

		for(%i = 0; %i < ClientGroup.getCount(); %i++) {
			%client = ClientGroup.getObject(%i);
			%client.votedInput = "";
		}

		fadeOutColorPKMNVI("00ffff", 1);
	}
	if(%cmd $= "m") {
		%bank = getField(%line, 1);
		%track = getField(%line, 2);

		talk("\c3BANK" SPC %bank SPC "\c2TRACK" SPC %track);

		$Server::Pokemon::CurMusicBank = %bank;
		$Server::Pokemon::CurMusicTrack = %track;
		%m = $Server::Pokemon::Music[$Server::Pokemon::CurMusicBank, $Server::Pokemon::CurMusicTrack];
		if(!isObject(%m)) {
			talk("WARNING: No music present for" SPC %m);
			return;
		}

		if(isObject($Server::Pokemon::Music)) {
			$Server::Pokemon::Music.delete();
		}
		if(%bank !$= "00") {
			$Server::Pokemon::Music = new AudioEmitter() {
				is3D = 0;
				profile = "musicData_PKMN_" @ $Server::Pokemon::CurMusicBank @ "_" @ $Server::Pokemon::CurMusicTrack;
				referenceDistance = 999999;
				maxDistance = 999999;
				volume = $Server::Pokemon::MusicVolume;
				position = $loadOffset;
			};
		}
	}
	if(%cmd $= "p") {
		//echo(%line);
		%whom = getField(%line, 1);
		if(!isObject(%whom)) {
			return;
		}

		if(jettisonParse(getFields(%line, 2))) {
			talk("Error in parsing JSON output");
			return;
		}

		%json = $JSON::Value;

		for(%idx = 0; %idx < %json.length; %idx++) {
			%data = %json.value[%idx];
			%name = %data.name;
			%species = %data.species;
			%level = %data.level;
			%hp = %data.hp.current SPC "/" SPC %data.hp.max;
			%atk = %data.stats.attack;
			%def = %data.stats.defense;
			%spd = %data.stats.speed;
			%spc = %data.stats.special;
			%status = %data.status;
			%type0 = %data.ptypes.value[0];
			%type1 = %data.ptypes.value[1];

			%moves = %data.moves;

			%typesStr = "<color:" @ $typeColor[%type0] @ ">" @ %type0;
			if(%type1 !$= %type0) {
				%typesStr = %typesStr SPC "\c6/" SPC "<color:" @ $typeColor[%type1] @ ">" @ %type1;
			}

			%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");
			%whom.chatMessage("<font:Courier New Bold:28>" @ (%name $= "" ? "\c4" @ %species : "\c4" @ %name @ " \c1(" @ %species @ ")") @ " \c7|| " @ %typesStr @ " \c7|| \c6Lv." @ %level @ " \c7|| \c2" @ %hp @ (%status $= "" ? "" : " \c0(" @ %status @ ")"));
			%whom.chatMessage("<font:Courier New Bold:28>\c7--\c3 ATK: " @ %atk @ "  \c3DEF: " @ %def @ "  \c3SPD: " @ %spd @ "  \c3SPC: " @ %spc);

			for(%mIdx = 0; %mIdx < %moves.length; %mIdx++) {
				%move = %moves.value[%mIdx];
				if(%move.move $= "") {
					continue;
				}
				%whom.chatMessage("<font:Courier New Bold:28>\c7---- \c5" @ %move.move @ "  \c6" @ %move.pp @ "pp");
			}
		}

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");
	}
	if(%cmd $= "s") {
		%bank = getField(%line, 1);
		%track = getField(%line, 2);
	
		%s = $Server::Pokemon::Sound[%bank,%track];
	
		if(isObject(%s)) {
			//talk("SOUND \c3" @ %bank @ "," @ %track);
			serverPlay2D(%s);
		}		
	}
	if(%cmd $= "t") {
		echo(%line);
		%whom = getField(%line, 1);
		if(!isObject(%whom)) {
			return;
		}

		if(jettisonParse(getFields(%line, 2))) {
			talk("Error in parsing JSON output");
			return;
		}

		%json = $JSON::Value;

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");

		for(%idx = 0; %idx < %json.length; %idx++) {
			%data = %json.value[%idx];
			%name = %data.name;
			%amount = %data.amount;

			if(%data.move !$= "") {
				%whom.chatMessage("<font:Courier New Bold:28><color:bbffff>" @ %name @ " \c4(" @ %data.move @ ") \c3x" @ %amount);
			} else {
				%whom.chatMessage("<font:Courier New Bold:28>\c6" @ %name @ "  \c3x" @ %amount);
			}
		}

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");
	}
	if(%cmd $= "w") {
		echo(%line);
		%whom = getField(%line, 1);
		if(!isObject(%whom)) {
			return;
		}

		if(jettisonParse(getFields(%line, 2))) {
			talk("Error in parsing JSON output");
			return;
		}

		%json = $JSON::Value;

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");

		%common = %json.common;
		%uncommon = %json.uncommon;
		%rare = %json.rare;

		%whom.chatMessage("<font:Courier New Bold:28><color:aa5500>COMMON");

		for(%idx = 0; %idx < %common.length; %idx++) {
			%pkmn = %common.value[%idx];
			if(%pkmn.name $= "" || %pkmn.level $= "0") {
				continue;
			}
			%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c4" @ %pkmn.name @ "  \c6Lv." @ %pkmn.level);
		}

		%whom.chatMessage("<font:Courier New Bold:28><color:cccccc>UNCOMMON");

		for(%idx = 0; %idx < %uncommon.length; %idx++) {
			%pkmn = %uncommon.value[%idx];
			if(%pkmn.name $= "" || %pkmn.level $= "0") {
				continue;
			}
			%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c4" @ %pkmn.name @ "  \c6Lv." @ %pkmn.level);
		}

		%whom.chatMessage("<font:Courier New Bold:28><color:ffdd00>RARE");

		for(%idx = 0; %idx < %rare.length; %idx++) {
			%pkmn = %rare.value[%idx];
			if(%pkmn.name $= "" || %pkmn.level $= "0") {
				continue;
			}
			%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c4" @ %pkmn.name @ "  \c6Lv." @ %pkmn.level);
		}

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");
	}
	if(%cmd $= "l") {
		echo(%line);
		%whom = getField(%line, 1);
		if(!isObject(%whom)) {
			return;
		}

		if(jettisonParse(getFields(%line, 2))) {
			talk("Error in parsing JSON output");
			return;
		}

		%json = $JSON::Value;

		%names = %json.names;
		%badges = %json.badges;
		%money = %json.money;
		%time = %json.time;
		%dex = %json.dex;

		%whom.chatMessage("<font:Courier New Bold:28>\c7=====================");
		%whom.chatMessage("<font:Courier New Bold:28>\c6YOU ARE \c2" @ %names.player);
		%whom.chatMessage("<font:Courier New Bold:28>\c6YOUR RIVAL IS \c0" @ %names.rival);
		%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c2$" @ %money);
		%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c3IGT " @ %time);
		%whom.chatMessage("<font:Courier New Bold:28>\c7-- \c1SEEN " @ %dex.seenCount SPC "\c4CAUGHT" SPC %dex.ownCount);

		%trainers = "GIOVANNI\tBLAINE\tSABRINA\tKOGA\tERIKA\tLT. SURGE\tMISTY\tBROCK";
		for(%i = 0; %i < 8; %i++) {
			%cTrainer = getField(%trainers, %i);
			%beaten[%i] = (getSubStr(%badges, %i, 1) $= "0" ? "\c0" : "\c2") @ %cTrainer @ getSubStr("           ", strLen(%cTrainer), 11);
		}

		%whom.chatMessage("<font:Courier New Bold:28>" @ %beaten[7] @ %beaten[6] @ %beaten[5] @ %beaten[4]);
		%whom.chatMessage("<font:Courier New Bold:28>" @ %beaten[3] @ %beaten[2] @ %beaten[1] @ %beaten[0]);
	}
	if(%cmd $= "a") {
		talk("Game saved to" SPC getField(%line, 1));
	}
}

function pingLoop() {
	cancel($pingLoopSched);
	$pingLoopSched = schedule(1000, 0, pingLoop);

	for(%i = 0; %i < ClientGroup.getCount(); %i++) {
		%client = ClientGroup.getObject(%i);
		%client.score = %client.getPing();
	}
}
pingLoop();

function PokemonTCPLines::send(%this, %data) {
	%list = %this;
	if(!$Server::Pokemon::Connected) {
		%list = PokemonTCPToProcess;
	}

	%list.addRow(getSimTime(), %data);

	if(!isEventPending(%this.checkToSendSched)) {
		%this.checkToSend();
	}
}

$PokemonIgnoreChecks = false;
function PokemonTCPLines::checkToSend(%this) {
	%other = PokemonTCPToProcess;
	%list = %this;
	if(%other.rowCount() > 0 && %this.rowCount() <= 0 && $Server::Pokemon::ProcessQueue) {
		%list = %other;
	}

	if(%other.rowCount() <= 0 && %this.rowCount() <= 0) {
		return;
	}

	if($Server::Pokemon::Connected) {
		%data = %list.getRowText(0);
		%list.removeRow(0);

		if(%data !$= "") {
			echo("\c5[SENT]\c0" SPC %data);
			PokemonTCPObject.send(%data @ "\r\n");
			%this.checkToSendSched = %this.schedule(1, checkToSend);
		}
	}
}

function serverCmdMode(%client, %mode) {
	if(!%client.isAdmin) {
		return;
	}

	$Server::Pokemon::InputMode = %mode;
	PokemonTCPLines.send("mode\t" @ %mode);

	talk("Input is now in" SPC (%mode $= "A" ? "\c0Anarchy" : "\c2Democracy") SPC "\c6mode");
}

function serverCmdParty(%client) {
	if($Sim::Time - %client.lastPartyCmd < 15) {
		return;
	}
	%client.lastPartyCmd = $Sim::Time;

	PokemonTCPLines.send("party\t" @ %client);
}

function serverCmdItems(%client) {
	if($Sim::Time - %client.lastItemsCmd < 15) {
		return;
	}
	%client.lastItemsCmd = $Sim::Time;

	PokemonTCPLines.send("items\t" @ %client);
}

function serverCmdWild(%client) {
	if($Sim::Time - %client.lastWildCmd < 15) {
		return;
	}
	%client.lastWildCmd = $Sim::Time;

	PokemonTCPLines.send("wild\t" @ %client);
}

function serverCmdPlayer(%client) {
	if($Sim::Time - %client.lastPlayerCmd < 15) {
		return;
	}
	%client.lastPlayerCmd = $Sim::Time;

	PokemonTCPLines.send("player\t" @ %client);
}

function serverCmdSave(%client) {
	if(%client.isAdmin) {
		PokemonTCPLines.send("save");
	}
}

function serverCmdEZCam(%client) {
	serverCmdDropCameraAtPlayer(%client);
	%client.camera.setTransform($Server::Pokemon::CenterCamPosition);
	if(isObject(%client.player)) {
		%client.player.setTransform("-10 -10 1");
	}
}

function GameConnection::hudLoop(%this) {
	%this.hudLoopSched = %this.schedule(333, hudLoop);
	%frames = ($Server::Pokemon::InputMode $= "A" ? "-" : $Server::Pokemon::FramesLeft);
	%this.bottomPrint("<font:Arial Bold:48><color:00ff00>" @ %frames @ "<just:center><font:Arial Bold:36><color:ffff00>" @ %this.votedInput @ "<just:right><font:Arial Bold:20>\c6Last input: <color:" @ $Server::Pokemon::InputColor @ ">" @ $Server::Pokemon::LastWinningInput @ " <br><just:left><font:Arial:20>\c6" @ mFloor($Server::Pokemon::ExpectedDelay) @ "ms / \c3" @ %this.getPing() @ "ms<just:right>\c4" @ getTimeString(mFloor($Sim::Time)) @ " ", 9, 1);
}

package PokemonPlaysPackage {
	function serverCmdMessageSent(%client, %msg) {
		if($Sim::Time - %client.lastInputTime < 0.1) {
			return;
		}
		%client.lastInputTime = $Sim::Time;

		PokemonTCPLines.send("vote" TAB %client TAB %msg);
		%client.isSpamming = 0;
		%client.spamMessageCount = 0;
		%p = parent::serverCmdMessageSent(%client, %msg);
		%client.lastChatText = sha1(getRandom(0, 999999) - %client.lastChatText);
		return %p;
	}

	function GameConnection::spawnPlayer(%this, %a, %b) {
		cancel(%this.hudLoopSched);
		%this.hudLoop();
		%this.chatMessage("\c6Input is currently in" SPC ($Server::Pokemon::InputMode $= "A" ? "\c0Anarchy" : "\c2Democracy") SPC "\c6mode");
		return parent::spawnPlayer(%this, %a, %b);
	}

	function spamAlert(%client) {
		%client.isSpamming = 0;
		%client.spamMessageCount = 0;

		return 0;		
	}

	function serverCmdPlantBrick(%client) {
		if(!%client.isAdmin) {
			%client.play2D(errorSound);
			%client.centerPrint("Building is disabled on this server.", 5);
			return;
		}
		return parent::serverCmdPlantBrick(%client);
	}

	function fxDTSBrick::onPlayerTouch(%this, %a) {
		%a.setTransform("-10 -10 1");
		return parent::onPlayerTouch(%this, %a);
	}

	function GameConnection::autoAdminCheck(%this) {
		commandToClient(%this, 'OpenBLPPGui');
		return parent::autoAdminCheck(%this);
	}
};
activatePackage(PokemonPlaysPackage);

package FreeTeleport
{
	function serverCmdDropCameraAtPlayer(%client)
	{
		%isAdmin = %client.isAdmin;
		%client.isAdmin = %isAdmin || !isObject(%client.miniGame);
		Parent::serverCmdDropCameraAtPlayer(%client);
		%client.isAdmin = %isAdmin;
	}

	function serverCmdDropPlayerAtCamera(%client)
	{
		%pos = %client.camera.getPosition();

		%min = $Server::Pokemon::Board[0, 0].getPosition();
		%max = $Server::Pokemon::Board[159, 143].getPosition();
		%x = getWord(%pos, 0);
		%y = getWord(%pos, 1);
		%minX = getWord(%min, 0);
		%maxX = getWord(%max, 0);
		%minY = getWord(%min, 1);
		%maxY = getWord(%max, 1);

		if((%x >= %minX && %x <= %maxX) && (%y >= %minY && %y <= %maxY)) {
			%client.play2D(errorSound);
			%client.centerPrint("You cannot drop your player over the screen.", 4);
			return;
		}

		%isAdmin = %client.isAdmin;
		%client.isAdmin = %isAdmin || !isObject(%client.miniGame);
		Parent::serverCmdDropPlayerAtCamera(%client);
		%client.isAdmin = %isAdmin;
	}

	function serverCmdFind(%client, %victimName)
	{
		%isAdmin = %client.isAdmin;

		if (!%isAdmin && !isObject(%client.miniGame))
		{
			%victimClient = findClientByName(%victimName);

			if (isObject(%victimClient) && !isObject(%victimClient.miniGame))
				%client.isAdmin = true;
		}

		Parent::serverCmdFind(%client, %victimName);
		%client.isAdmin = %isAdmin;
	}

	function serverCmdWarp(%client)
	{
		%isAdmin = %client.isAdmin;
		%client.isAdmin = %isAdmin || !isObject(%client.miniGame);
		Parent::serverCmdWarp(%client);
		%client.isAdmin = %isAdmin;
	}
};

activatePackage("FreeTeleport");

$SPAM_PROTECTION_PERIOD = 0;
$SPAM_MESSAGE_THRESHOLD = 999999;
$SPAM_PENALTY_PERIOD    = 0;

function serverCmdHelp(%client) {
	%client.chatMessage("\c7===============\c4 HELP \c7===============");
	%client.chatMessage("\c6Use the chat to control the game! \c7(TODO: events for bricks)");
	%client.chatMessage("\c2up / down / left / right / b / a / start / select \c6(shorthand messages work too! like \c2u/d/l/r/b/a/st/se\c6)");
	if($Server::Pokemon::InputMode $= "A") {
		%client.chatMessage("\c6Inputs are currently being sent straight to the game (\c0Anarchy\c6 Mode)");
	} else {
		%client.chatMessage("\c6Only the most popular input within a period of time is being sent to the game (\c2Democracy\c6 Mode)");
	}
	%client.chatMessage("\c7=============\c4 COMMANDS \c7=============");
	%client.chatMessage("\c5/party \c7-- \c6Shows the current Pokemon in your party.");
	%client.chatMessage("\c5/items \c7-- \c6View items currently in your inventory.");
	%client.chatMessage("\c5/wild \c7-- \c6View the wild Pokemon that can be caught in this area.");
	%client.chatMessage("\c5/player \c7-- \c6View your player information.");
	%client.chatMessage("\c5/ezcam \c7-- \c6Centers your camera over the game screen.");
	%client.chatMessage("\c5/faq \c7-- \c6Common questions, answered.");

	if(%client.isAdmin) {
		%client.chatMessage("\c4/mode \c2A|D \c7-- \c6Sets the input mode to Anarchy or Democracy.");
		%client.chatMessage("\c4/save \c7-- \c6Manually saves the game.");
		%client.chatMessage("\c4/modPalette \c20-3 red0-255 blue0-255 green0-255 \c7-- \c6Sets the screen's color palette.");
		%client.chatMessage("\c4/savePalette \c7-- \c6Saves the current screen palette.");
		%client.chatMessage("\c4/loadPalette \c2## \c7-- \c6Load a saved palette.");
		%client.chatMessage("\c4/viewPalette \c2[##] \c7-- \c6View a specific saved palette, or view all of them.");
		%client.chatMessage("\c4/refreshScreen \c7-- \c6Force a screen refresh.");
	}

	%client.chatMessage("\c3All game-related commands have a 15 second cooldown.");
	%client.chatMessage("\c7------- \c5You may need to use PAGE UP to view lines above. \c7-------");
}

function serverCmdFaq(%client) {
	%client.chatMessage("\c7================\c4 FAQ \c7===============");
	%client.chatMessage("\c2How are you doing this?");
	%client.chatMessage("\c6Blockland is essentially acting as a viewer for a separate NodeJS server. Blockland is listening to a TCP server from NodeJS's side constantly.");
	%client.chatMessage("\c6As for an emulator, it's running the latest version of WasmBoy in headless mode.");
	%client.chatMessage("\c2Why is the screen updating so slowly?");
	%client.chatMessage("\c6It's just how much Blockland can handle at once. Not only does it have to update 23,040 bricks, it has to parse information for all of them as well.");
	%client.chatMessage("\c6Every refresh, Blockland recieves about 7KB in data from WasmBoy's screen memory. (3 pixels on the X axis in 1 byte, 144 rows of it)");
	%client.chatMessage("\c2How big is the screen?");
	%client.chatMessage("\c6160x144, 4 colors per pixel.");
	%client.chatMessage("\c2How is music and sound working?");
	%client.chatMessage("\c6I'm watching memory addresses in Pokemon and sending out update commands every time it changes, as well as watching an elapsed value in SFX addresses.");
	%client.chatMessage("\c6This is why sound occasionally duplicates, by my logic, it thinks sound has been restarted, so it plays the sound over and over. Nothing I can do that I'm aware of to fix it.");
	%client.chatMessage("\c2What version of the game are you running?");
	%client.chatMessage("\c6The Red version, with a patch that allows \c3every \c6Pokemon to be caught.");
	%client.chatMessage("\c2Where are the Pokemon cries? The other sound effects?");
	%client.chatMessage("\c6I'd need a way to programatically render out audio from the ROM and I don't know how to do that, unfortunately. It's a bit out of my league.");
	%client.chatMessage("\c6Plus a LOT of sounds would (annoyingly) duplicate, namely the cries. I tried to get the basic SFX at the least.");
}