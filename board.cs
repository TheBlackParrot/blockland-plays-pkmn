$Server::Pokemon::PrintID = 36;

function buildPokemonBoard() {
	if($Server::Pokemon::BoardBuilt !$= "") {
		return;
	}
	$Server::Pokemon::BoardBuilt = 1;

	for(%x = 0; %x < 160; %x++) {
		for(%y = 0; %y < 144; %y++) {
			%b = $Server::Pokemon::Board[%x,%y] = new fxDTSBrick() {
				angleID = 1;
				client = -1;
				colorFxID = 0;
				colorID = 0;
				dataBlock = "brick1x1fPrintData";
				position = %x * 0.5 SPC %y * 0.5 SPC 0.1;
				rotation = "0 0 1 90";
				scale = "1 1 1";
				shapeFxID = 0;
				stackBL_ID = -1;
				enableTouch = 1;
				printID = $Server::Pokemon::PrintID;
			};

			BrickGroup_888888.add(%b);
			%b.setTrusted(1);
			%b.plant();
		}
	}

	PokemonTCPLines.send("done");
}

// 0 -- aggressivest
// 1 -- aggressiver
// 2 -- aggressive
// 3 -- relaxed (little ghosting)
$Server::Pokemon::RenderMode = 0;

function renderScreen(%y) {
	if(%y == 0) {
		//talk("Starting render...");
		$Server::Pokemon::Start = $Sim::Time;
	}

	switch($Server::Pokemon::RenderMode) {
		case 0: %d = %y % 2;
		case 1: %d = %y % 2;
		case 2: %d = 1;
		case 3: %d = 10;
	}

	if(%y < 144) {
		schedule(%d, 0, renderScreen, %y+1);
	} else {
		actuallyRenderScreen(0);
	}

	for(%x = 0; %x < 54; %x++) {
		%value = $pchr[getSubStr($Server::Pokemon::ScreenData, mAbs(%x - 53) + (%y * 54), 1)];
		$Server::Pokemon::Px[(%x*3)-2,%y] = (%value >> 4) % 4;
		$Server::Pokemon::Px[(%x*3)-1,%y] = (%value >> 2) % 4;
		$Server::Pokemon::Px[(%x*3),%y] = %value % 4;
	}
}

function actuallyRenderScreen(%y) {
	if(%y < 144) {
		switch($Server::Pokemon::RenderMode) {
			case 0: %d = %y % 2;
			case 1: %d = 1;
			case 2: %d = 1;
			case 3: %d = 10;
		}

		schedule(%d, 0, actuallyRenderScreen, %y+1);
	} else {
		//talk("Finished in" SPC getTimeString($Sim::Time - $Server::Pokemon::Start) @ "!");
		$Server::Pokemon::ExpectedDelay = (($Sim::Time - $Server::Pokemon::Start)*1000) + 33;
		PokemonTCPLines.send("done");
	}

	for(%x = 0; %x < 160; %x++) {
		if($Server::Pokemon::Px[%x,%y] == $Server::Pokemon::BoardCol[%x,%y]) {
			continue;
		}
		
		$Server::Pokemon::BoardCol[%x,%y] = $Server::Pokemon::Px[%x,%y];
		$Server::Pokemon::Board[%x,%y].setColor($Server::Pokemon::Px[%x,%y]);
	}
}

function forceScreenRefresh() {
	for(%y = 0; %y < 144; %y++) {
		for(%x = 0; %x < 160; %x++) {
			$Server::Pokemon::Px[%x,%y] = 4;
			$Server::Pokemon::BoardCol[%x,%y] = 63;
			$Server::Pokemon::Board[%x,%y].setColor(0);
		}
	}
}

function serverCmdModPalette(%client, %which, %r, %g, %b, %mode) {
	if(!%client.isAdmin) {
		return;
	}

	if((%mode $= "" || %mode $= "rgb" || %mode $= "r") && %b !$= "") {
		%str = %r/255 SPC %g/255 SPC %b/255 SPC 1;
	} else {
		if(%g $= "hex" || %g $= "h") {
			%str = _BLPP_HexToRGB(%r);
		}
	}

	setColorTable(%which, %str);

	for(%i = 0; %i < ClientGroup.getCount(); %i++) {
		%cl = ClientGroup.getObject(%i);

		%cl.transmitStaticBrickData();
		commandToClient(%cl, 'PlayGui_LoadPaint');
	}

	%client.chatMessage("\c6Set \c3color" SPC %which SPC "\c6to\c4" SPC %str);

	forceScreenRefresh();
}

if(isFile("config/server/BLPP/palettes.cs")) {
	exec("config/server/BLPP/palettes.cs");
} else {
	$Server::Pokemon::Palette0 = "0.000000 0.000000 0.000000 1.000000\t0.337255 0.337255 0.337255 1.000000\t0.674510 0.674510 0.674510 1.000000\t1.000000 1.000000 1.000000 1.000000";
	$Server::Pokemon::PaletteCount = 1;

	export("$Server::Pokemon::Palette*", "config/server/BLPP/palettes.cs");
}

function serverCmdSavePalette(%client) {
	if(!%client.isAdmin) {
		return;
	}

	%str = getColorIDTable(0) TAB getColorIDTable(1) TAB getColorIDTable(2) TAB getColorIDTable(3);

	for(%i = 0; %i < $Server::Pokemon::PaletteCount; %i++) {
		if(%str $= $Server::Pokemon::Palette[%i]) {
			%client.chatMessage("\c0This palette already exists! See #" @ %i);
			%client.play2D(errorSound);
			return;
		}
	}

	$Server::Pokemon::Palette[$Server::Pokemon::PaletteCount] = %str;
	$Server::Pokemon::PaletteCount++;

	export("$Server::Pokemon::Palette*", "config/server/BLPP/palettes.cs");

	%client.chatMessage("\c6Saved palette to slot" SPC $Server::Pokemon::PaletteCount-1);
}

function serverCmdLoadPalette(%client, %which) {
	if(!%client.isAdmin) {
		if($Sim::Time - %client.lastPaletteLoadCmd < 2000) {
			return;
		}
		%client.lastPaletteLoadCmd = $Sim::Time;
	}

	if(!%client.isAdmin && ClientGroup.getCount() > 1) {
		return;
	}

	if(ClientGroup.getCount() == 1) {
		%client.chatMessage("\c6You are allowed to use this command since you are the only one online. :)");
	}

	%wants = $Server::Pokemon::Palette[%which];
	if(%wants $= "") {
		%client.chatMessage("\c0This palette is blank?");
		%client.play2D(errorSound);
		return;
	}

	for(%i = 0; %i < 4; %i++) {
		setColorTable(%i, getField(%wants, %i));
	}

	for(%i = 0; %i < ClientGroup.getCount(); %i++) {
		%cl = ClientGroup.getObject(%i);

		%cl.transmitStaticBrickData();
		commandToClient(%cl, 'PlayGui_LoadPaint');
	}

	%client.chatMessage("\c6Loaded palette #" @ %which);

	forceScreenRefresh();
}

exec("./lib/rgbtohex.cs");

function serverCmdViewPalette(%client, %which) {
	if(!%client.isAdmin) {
		if($Sim::Time - %client.lastPaletteViewCmd < 2000) {
			return;
		}
		%client.lastPaletteViewCmd = $Sim::Time;
	}

	if(%which $= "") {
		for(%j = 0; %j < $Server::Pokemon::PaletteCount; %j++) {
			%str = "";

			for(%i = 0; %i < 4; %i++) {
				%col = _BLPP_RGBToHex(getField($Server::Pokemon::Palette[%j], %i));
				%str = trim(%str SPC "<color:" @ %col @ ">" @ %col);
			}
			%str = "<font:Courier New Bold:28><div:1>\c6" @ %j @ "." SPC %str;

			%client.chatMessage(%str);
		}
	} else {
		%str = "";

		%wants = $Server::Pokemon::Palette[%which];
		if(%wants $= "") {
			%client.chatMessage("\c0This palette is blank?");
			%client.play2D(errorSound);
			return;
		}

		for(%i = 0; %i < 4; %i++) {
			%col = _BLPP_RGBToHex(getField(%wants, %i));
			%str = trim(%str SPC "<color:" @ %col @ ">" @ %col);
		}
		%str = "<font:Courier New Bold:28><div:1>" @ %str;

		%client.chatMessage(%str);
	}
}

function serverCmdRefreshScreen(%client) {
	if(!%client.isAdmin) {
		return;
	}
	forceScreenRefresh();
}