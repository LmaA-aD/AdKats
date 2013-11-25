-- AdKats Database Setup Script by ColColonCleaner

-- Start Tables

SET FOREIGN_KEY_CHECKS=0;

DROP TABLE IF EXISTS `adkats_commands`;
CREATE TABLE IF NOT EXISTS `adkats_commands` (
	`command_id` int(11) unsigned NOT NULL,
	`command_active` enum('Active', 'Disabled', 'Invisible') NOT NULL DEFAULT 'Active',
	`command_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
	`command_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
	`command_text` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
	PRIMARY KEY (`command_id`),
	UNIQUE KEY `command_key_UNIQUE` (`command_key`),
	UNIQUE KEY `command_text_UNIQUE` (`command_text`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Command List';

DROP TABLE IF EXISTS `adkats_roles`;
CREATE TABLE IF NOT EXISTS `adkats_roles` (
	`role_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
	`role_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
	`role_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
	PRIMARY KEY (`role_id`),
	UNIQUE KEY `role_key_UNIQUE` (`role_key`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Role List';

DROP TABLE IF EXISTS `adkats_rolecommands`;
CREATE TABLE IF NOT EXISTS `adkats_rolecommands` (
	`role_id` int(11) unsigned NOT NULL,
	`command_id` int(11) unsigned NOT NULL,
	PRIMARY KEY (`role_id`,`command_id`),
	KEY `adkats_rolecommands_fk_role` (`role_id`),
	KEY `adkats_rolecommands_fk_command` (`command_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of commands to roles';

DROP TABLE IF EXISTS `adkats_users`;
CREATE TABLE IF NOT EXISTS `adkats_users` (
	`user_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
	`user_name` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
	`user_email` varchar(255) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'test@gmail.com',
	`user_role` int(11) unsigned NOT NULL DEFAULT '1',
	PRIMARY KEY (`user_id`),
	KEY `adkats_users_fk_role` (`user_role`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - User List';

DROP TABLE IF EXISTS `adkats_usersoldiers`;
CREATE TABLE IF NOT EXISTS `adkats_usersoldiers` (
	`user_id` int(11) unsigned NOT NULL,
	`player_id` int(10) unsigned NOT NULL,
	PRIMARY KEY (`user_id`,`player_id`),
	KEY `adkats_usersoldiers_fk_user` (`user_id`),
	KEY `adkats_usersoldiers_fk_player` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of users to soldiers';

DROP TABLE IF EXISTS `adkats_records_main`;
CREATE TABLE IF NOT EXISTS `adkats_records_main` (
	`record_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`command_type` INT(11) UNSIGNED NOT NULL, 
	`command_action` INT(11) UNSIGNED NOT NULL, 
	`command_numeric` INT(11) NOT NULL DEFAULT 0, 
	`target_name` VARCHAR(45) NOT NULL DEFAULT "NoTarget", 
	`target_id` INT(11) UNSIGNED DEFAULT NULL, 
	`source_name` VARCHAR(45) NOT NULL DEFAULT "NoSource", 
	`source_id` INT(11) UNSIGNED DEFAULT NULL, 
	`record_message` VARCHAR(500) NOT NULL DEFAULT "NoMessage", 
	`record_time` TIMESTAMP NOT NULL, 
	`adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`adkats_web` BOOL NOT NULL DEFAULT 0,
	PRIMARY KEY (`record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Main Records';

DROP TABLE IF EXISTS `adkats_records_debug`;
CREATE TABLE IF NOT EXISTS `adkats_records_debug` (
	`record_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL,  
	`command_type` INT(11) UNSIGNED NOT NULL, 
	`command_action` INT(11) UNSIGNED NOT NULL,
	`command_numeric` INT(11) NOT NULL DEFAULT 0, 
	`target_name` VARCHAR(45) NOT NULL DEFAULT "NoTarget", 
	`target_id` INT(11) UNSIGNED DEFAULT NULL, 
	`source_name` VARCHAR(45) NOT NULL DEFAULT "NoSource", 
	`source_id` INT(11) UNSIGNED DEFAULT NULL, 
	`record_message` VARCHAR(500) NOT NULL DEFAULT "NoMessage", 
	`record_time` TIMESTAMP NOT NULL, 
	`adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`adkats_web` BOOL NOT NULL DEFAULT 0,
	PRIMARY KEY (`record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Debug Records';

DROP TABLE IF EXISTS `adkats_infractions_server`;
CREATE TABLE IF NOT EXISTS `adkats_infractions_server` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`, `server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Server Specific Player Infraction Points';

DROP TABLE IF EXISTS `adkats_infractions_global`;
CREATE TABLE IF NOT EXISTS `adkats_infractions_global` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Global Player Infraction Points';

DROP TABLE IF EXISTS `adkats_bans`;
CREATE TABLE IF NOT EXISTS `adkats_bans` ( 
	`ban_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`latest_record_id` INT(11) UNSIGNED NOT NULL, 
	`ban_notes` VARCHAR(150) NOT NULL DEFAULT 'NoNotes', 
	`ban_status` enum('Active', 'Expired', 'Disabled') NOT NULL DEFAULT 'Active',
	`ban_startTime` TIMESTAMP NOT NULL, 
	`ban_endTime` DATETIME NOT NULL, 
	`ban_enforceName` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_enforceGUID` ENUM('Y', 'N') NOT NULL DEFAULT 'Y', 
	`ban_enforceIP` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_sync` VARCHAR(100) NOT NULL DEFAULT "-sync-", 
	PRIMARY KEY (`ban_id`), 
	UNIQUE KEY `player_id_UNIQUE` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Ban List';

DROP TABLE IF EXISTS `adkats_settings`;
CREATE TABLE IF NOT EXISTS `adkats_settings` ( 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`setting_name` VARCHAR(200) NOT NULL DEFAULT "SettingName", 
	`setting_type` VARCHAR(45) NOT NULL DEFAULT "SettingType", 
	`setting_value` VARCHAR(1500) NOT NULL DEFAULT "SettingValue", 
	PRIMARY KEY (`server_id`, `setting_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Server Setting List';

-- End Tables

-- Start Data

INSERT INTO `adkats_commands` (`command_id`, `command_active`, `command_key`, `command_name`, `command_text`) VALUES
	(1, 'Active', 'command_confirm', 'Confirm Command', 'yes'),
	(2, 'Active', 'command_cancel', 'Cancel Command', 'no'),
	(3, 'Active', 'player_kill', 'Kill Player', 'kill'),
	(4, 'Invisible', 'player_kill_lowpop', 'Kill Player (Low Population)', 'lowpopkill'),
	(5, 'Invisible', 'player_kill_repeat', 'Kill Player (Repeat Kill)', 'repeatkill'),
	(6, 'Active', 'player_kick', 'Kick Player', 'kick'),
	(7, 'Active', 'player_tban', 'Temp-Ban Player', 'tban'),
	(8, 'Active', 'player_ban', 'Permaban Player', 'ban'),
	(9, 'Active', 'player_punish', 'Punish Player', 'punish'),
	(10, 'Active', 'player_forgive', 'Forgive Player', 'forgive'),
	(11, 'Active', 'player_mute', 'Mute Player', 'mute'),
	(12, 'Active', 'player_join', 'Join Player', 'join'),
	(13, 'Active', 'player_roundwhitelist', 'Round Whitelist Player', 'roundwhitelist'),
	(14, 'Active', 'player_move', 'On-Death Move Player', 'move'),
	(15, 'Active', 'player_fmove', 'Force Move Player', 'fmove'),
	(16, 'Active', 'self_teamswap', 'Teamswap Self', 'moveme'),
	(17, 'Active', 'self_kill', 'Kill Self', 'killme'),
	(18, 'Active', 'player_report', 'Report Player', 'report'),
	(19, 'Invisible', 'player_report_confirm', 'Report Player (Confirmed)', 'confirmreport'),
	(20, 'Active', 'player_calladmin', 'Call Admin on Player', 'admin'),
	(21, 'Active', 'admin_say', 'Admin Say', 'say'),
	(22, 'Active', 'player_say', 'Player Say', 'psay'),
	(23, 'Active', 'admin_yell', 'Admin Yell', 'yell'),
	(24, 'Active', 'player_yell', 'Player Yell', 'pyell'),
	(25, 'Active', 'admin_tell', 'Admin Tell', 'tell'),
	(26, 'Active', 'player_tell', 'Player Tell', 'ptell'),
	(27, 'Active', 'self_whatis', 'What Is', 'whatis'),
	(28, 'Active', 'self_voip', 'VOIP', 'voip'),
	(29, 'Active', 'self_rules', 'Request Rules', 'rules'),
	(30, 'Active', 'round_restart', 'Restart Current Round', 'restart'),
	(31, 'Active', 'round_next', 'Run Next Round', 'nextlevel'),
	(32, 'Active', 'round_end', 'End Current Round', 'endround'),
	(33, 'Active', 'server_nuke', 'Server Nuke', 'nuke'),
	(34, 'Active', 'server_kickall', 'Kick All Guests', 'kickall'),
	(35, 'Invisible', 'adkats_exception', 'Logged Exception', 'logexception'),
	(36, 'Invisible', 'banenforcer_enforce', 'Enforce Active Ban', 'enforceban');

INSERT INTO `adkats_roles` (`role_id`, `role_key`, `role_name`) VALUES
	(1, 'guest_default', 'Default Guest'),
	(2, 'admin_full', 'Full Admin');

INSERT INTO `adkats_rolecommands` (`role_id`, `command_id`) VALUES
	(1, 1),
	(1, 2),
	(1, 12),
	(1, 17),
	(1, 18),
	(1, 20),
	(1, 27),
	(1, 28),
	(1, 29),
	(2, 1),
	(2, 2),
	(2, 3),
	(2, 4),
	(2, 5),
	(2, 6),
	(2, 7),
	(2, 8),
	(2, 9),
	(2, 10),
	(2, 11),
	(2, 12),
	(2, 13),
	(2, 14),
	(2, 15),
	(2, 16),
	(2, 17),
	(2, 18),
	(2, 19),
	(2, 20),
	(2, 21),
	(2, 22),
	(2, 23),
	(2, 24),
	(2, 25),
	(2, 26),
	(2, 27),
	(2, 28),
	(2, 29),
	(2, 30),
	(2, 31),
	(2, 32),
	(2, 33),
	(2, 34),
	(2, 35),
	(2, 36);
	
-- End Data

SET FOREIGN_KEY_CHECKS=1;

-- Start Constraints

ALTER TABLE `adkats_rolecommands`
	ADD CONSTRAINT `adkats_rolecommands_fk_role` FOREIGN KEY (`role_id`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_rolecommands_fk_command` FOREIGN KEY (`command_id`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_users`
  ADD CONSTRAINT `adkats_users_fk_role` FOREIGN KEY (`user_role`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_usersoldiers`
	ADD CONSTRAINT `adkats_usersoldiers_fk_player` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_usersoldiers_fk_user` FOREIGN KEY (`user_id`) REFERENCES `adkats_users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE;
  
ALTER TABLE `adkats_records_main` 
	ADD CONSTRAINT `adkats_records_main_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_main_fk_command_type` FOREIGN KEY (`command_type`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_main_fk_command_action` FOREIGN KEY (`command_action`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_main_fk_target_id` FOREIGN KEY (`target_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_main_fk_source_id` FOREIGN KEY (`source_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE SET NULL ON UPDATE CASCADE;
	
ALTER TABLE `adkats_records_debug` 
	ADD CONSTRAINT `adkats_records_debug_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_debug_fk_command_type` FOREIGN KEY (`command_type`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_records_debug_fk_command_action` FOREIGN KEY (`command_action`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE;
	
ALTER TABLE `adkats_infractions_server` 
	ADD CONSTRAINT `adkats_infractions_server_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_infractions_server_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_infractions_global` 
	ADD CONSTRAINT `adkats_infractions_global_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_bans` 
	ADD CONSTRAINT `adkats_bans_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
	ADD CONSTRAINT `adkats_bans_fk_latest_record_id` FOREIGN KEY (`latest_record_id`) REFERENCES `adkats_records_main` (`record_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_settings` 
	ADD CONSTRAINT `adkats_settings_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE;

-- End Constraints

-- Start Triggers

DROP TRIGGER IF EXISTS adkats_infraction_point_insert;
DROP TRIGGER IF EXISTS adkats_infraction_point_delete;

DELIMITER |

CREATE TRIGGER adkats_infraction_point_insert BEFORE INSERT ON `adkats_records_main`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = NEW.command_type;
		SET server_id = NEW.server_id;
		SET player_id = NEW.target_id;

		IF(command_type = 9) THEN
			INSERT INTO `adkats_infractions_server` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_infractions_global` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
		ELSEIF (command_type = 10) THEN
			INSERT INTO `adkats_infractions_server` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_infractions_global` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
		END IF;
	END;

|

CREATE TRIGGER adkats_infraction_point_delete AFTER DELETE ON `adkats_records_main`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = OLD.command_type;
		SET server_id = OLD.server_id;
		SET player_id = OLD.target_id;

		IF(command_type = 9) THEN
			INSERT INTO `adkats_infractions_server` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_infractions_global` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
		ELSEIF (command_type = 10) THEN
			INSERT INTO `adkats_infractions_server` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_infractions_global` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
		END IF;
	END;

|

DELIMITER ;

-- End Triggers
