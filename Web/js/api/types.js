// Generated from backend DTOs by scripts/contracts.mjs --write; check snapshot before deployment.
// Backend/Harekat.Application/Dtos/Dtos.cs
/**
 * @typedef {Object} RegisterRequest
 * @property {string} username
 * @property {string} email
 * @property {string} password
 * @property {(string|null)} region
 */
/**
 * @typedef {Object} LoginRequest
 * @property {string} username
 * @property {string} password
 */
/**
 * @typedef {Object} SteamAuthRequest
 * @property {string} ticket
 * @property {(string|null)} personaName
 * @property {(string|null)} region
 */
/**
 * @typedef {Object} AuthResponse
 * @property {string} accessToken
 * @property {string} refreshToken
 * @property {string} expiresAt
 * @property {PlayerDto} player
 */
/**
 * @typedef {Object} RefreshRequest
 * @property {string} refreshToken
 */
/**
 * @typedef {Object} VerifyEmailRequest
 * @property {string} token
 */
/**
 * @typedef {Object} PlayerDto
 * @property {string} id
 * @property {string} username
 * @property {string} email
 * @property {CareerStats} stats
 * @property {number} rank
 * @property {number} eloRating
 * @property {(string|null)} squadId
 * @property {boolean} emailVerified
 * @property {string} role
 * @property {boolean} isOnline
 * @property {string} region
 * @property {Array<string>} ownedCosmetics
 * @property {string} equippedCamo
 * @property {string} equippedBeret
 * @property {Array<string>} unlockedAchievements
 * @property {number} seasonXp
 */
/**
 * @typedef {Object} CreateSquadRequest
 * @property {string} name
 * @property {(string|null)} region
 */
/**
 * @typedef {Object} InviteSquadRequest
 * @property {string} inviteCode
 */
/**
 * @typedef {Object} SquadDto
 * @property {string} id
 * @property {string} name
 * @property {string} leaderId
 * @property {Array<string>} memberIds
 * @property {Array<string>} readyMemberIds
 * @property {string} inviteCode
 * @property {string} region
 * @property {boolean} allReady
 * @property {number} openSlots
 */
/**
 * @typedef {Object} QueueRequest
 * @property {(string|null)} region
 * @property {(number|null)} maxPingMs
 */
/**
 * @typedef {Object} QueueResponse
 * @property {string} ticketId
 * @property {string} status
 * @property {string} region
 * @property {string} enqueuedAt
 */
/**
 * @typedef {Object} MatchDto
 * @property {string} id
 * @property {string} region
 * @property {string} status
 * @property {(string|null)} serverEndpoint
 * @property {Array<MatchTeamDto>} teams
 * @property {number} seasonNumber
 */
/**
 * @typedef {Object} MatchTeamDto
 * @property {string} squadId
 * @property {string} squadName
 * @property {Array<string>} playerIds
 * @property {number} botCount
 * @property {number} placement
 */
/**
 * @typedef {Object} RegisterServerRequest
 * @property {string} host
 * @property {number} port
 * @property {string} region
 * @property {string} serverKey
 * @property {number} maxPlayers
 */
/**
 * @typedef {Object} ServerHeartbeatRequest
 * @property {string} serverId
 * @property {string} serverKey
 * @property {number} currentPlayers
 * @property {string} status
 */
/**
 * @typedef {Object} ServerDto
 * @property {string} id
 * @property {string} endpoint
 * @property {string} region
 * @property {string} status
 * @property {number} currentPlayers
 * @property {number} maxPlayers
 */
/**
 * @typedef {Object} ClaimMatchRequest
 * @property {string} host
 * @property {number} port
 * @property {string} serverKey
 * @property {(string|null)} region
 * @property {number} maxPlayers
 */
/**
 * @typedef {Object} ReleaseServerRequest
 * @property {string} serverId
 * @property {string} serverKey
 */
/**
 * @typedef {Object} PlayerMatchResultDto
 * @property {string} playerId
 * @property {number} kills
 * @property {number} headshots
 * @property {number} damage
 * @property {number} survivalSeconds
 * @property {(string|null)} topWeaponId
 */
/**
 * @typedef {Object} TeamMatchResultDto
 * @property {string} squadId
 * @property {number} placement
 * @property {Array<PlayerMatchResultDto>} players
 */
/**
 * @typedef {Object} MatchResultRequest
 * @property {Array<TeamMatchResultDto>} teams
 */
/**
 * @typedef {Object} MatchResultResponse
 * @property {string} matchId
 * @property {Array<XpAwardDto>} awards
 */
/**
 * @typedef {Object} XpAwardDto
 * @property {string} playerId
 * @property {number} xpGained
 * @property {number} newRank
 * @property {number} newElo
 * @property {Array<string>} newAchievements
 */
/**
 * @typedef {Object} LeaderboardEntryDto
 * @property {number} rank
 * @property {string} playerId
 * @property {string} username
 * @property {number} militaryRank
 * @property {number} value
 * @property {number} elo
 */
/**
 * @typedef {Object} FriendshipDto
 * @property {string} id
 * @property {string} otherPlayerId
 * @property {string} otherUsername
 * @property {string} status
 * @property {boolean} otherOnline
 */
/**
 * @typedef {Object} FriendRequestDto
 * @property {string} targetPlayerId
 */
/**
 * @typedef {Object} SeasonDto
 * @property {number} number
 * @property {string} name
 * @property {string} startsAt
 * @property {string} endsAt
 * @property {boolean} isActive
 */
/**
 * @typedef {Object} SeasonArchiveDto
 * @property {number} seasonNumber
 * @property {string} playerId
 * @property {string} username
 * @property {number} seasonXp
 * @property {number} placement
 * @property {string} rewardBadge
 */
/**
 * @typedef {Object} AchievementDto
 * @property {string} id
 * @property {string} title
 * @property {string} description
 * @property {number} target
 * @property {number} progress
 * @property {boolean} unlocked
 */
/**
 * @typedef {Object} CosmeticDto
 * @property {string} id
 * @property {string} name
 * @property {string} slot
 * @property {boolean} owned
 * @property {boolean} equipped
 */
/**
 * @typedef {Object} EquipCosmeticRequest
 * @property {string} cosmeticId
 */
/**
 * @typedef {Object} ReportPlayerRequest
 * @property {string} reportedPlayerId
 * @property {string} reason
 * @property {(string|null)} matchId
 */
/**
 * @typedef {Object} BanPlayerRequest
 * @property {string} playerId
 * @property {string} reason
 * @property {(number|null)} durationHours
 */
/**
 * @typedef {Object} MutePlayerRequest
 * @property {string} playerId
 * @property {number} durationHours
 * @property {(string|null)} reason
 */
/**
 * @typedef {Object} LobbyChatMessage
 * @property {string} squadId
 * @property {string} senderId
 * @property {string} senderName
 * @property {string} message
 * @property {string} sentAt
 */
/**
 * @typedef {Object} ReadyStatusDto
 * @property {string} squadId
 * @property {string} playerId
 * @property {boolean} isReady
 * @property {boolean} allReady
 */
/**
 * @typedef {Object} NewsItemDto
 * @property {string} id
 * @property {string} title
 * @property {string} body
 * @property {string} language
 * @property {(string|null)} author
 * @property {string} publishedAt
 * @property {number} sortOrder
 */
/**
 * @typedef {Object} UpsertNewsRequest
 * @property {string} title
 * @property {string} body
 * @property {(string|null)} language
 * @property {(string|null)} author
 * @property {boolean} isPublished
 * @property {number} sortOrder
 * @property {(string|null)} publishedAt
 */
/**
 * @typedef {Object} ClientVersionDto
 * @property {string} channel
 * @property {string} version
 * @property {string} patchUrl
 * @property {string} sha256
 * @property {number} patchSizeBytes
 * @property {(string|null)} releaseNotes
 * @property {boolean} mandatory
 * @property {string} publishedAt
 */
/**
 * @typedef {Object} UpsertClientVersionRequest
 * @property {string} version
 * @property {string} patchUrl
 * @property {string} sha256
 * @property {(number|null)} patchSizeBytes
 * @property {(string|null)} releaseNotes
 * @property {boolean} mandatory
 * @property {(string|null)} channel
 */
// Backend/Harekat.Domain/ValueObjects/CareerStats.cs
/**
 * @typedef {Object} CareerStats
 * @property {number} matches
 * @property {number} wins
 * @property {number} kills
 * @property {number} headshots
 * @property {number} bestPlacement
 * @property {number} totalDamage
 * @property {number} longestSurvivalSeconds
 * @property {number} experience
 * @property {number} rank
 */
// Backend/Harekat.Telemetry/src/Harekat.Telemetry.Application/Contracts/Dtos.cs
/**
 * @typedef {Object} EventBatchRequest
 * @property {string} matchId
 * @property {Array<MatchEventDto>} events
 */
/**
 * @typedef {Object} MatchEventDto
 * @property {string} playerId
 * @property {number} eventType
 * @property {(string|null)} timestamp
 * @property {(number|null)} x
 * @property {(number|null)} y
 * @property {(number|null)} z
 * @property {(number|null)} targetX
 * @property {(number|null)} targetY
 * @property {(number|null)} targetZ
 * @property {(string|null)} targetPlayerId
 * @property {(string|null)} weaponId
 * @property {boolean} isHeadshot
 * @property {boolean} throughWall
 * @property {(number|null)} damage
 * @property {(number|null)} timeToKillMs
 * @property {(number|null)} distanceMeters
 */
/**
 * @typedef {Object} EventBatchResponse
 * @property {string} matchId
 * @property {number} accepted
 * @property {number} rejected
 * @property {Array<string>} errors
 * @property {Array<PlayerSuspicionSummaryDto>} suspicionSummaries
 */
/**
 * @typedef {Object} PlayerSuspicionSummaryDto
 * @property {string} playerId
 * @property {number} score
 * @property {boolean} isSuspect
 * @property {number} findingCount
 */
/**
 * @typedef {Object} SuspicionReportDto
 * @property {string} playerId
 * @property {string} matchId
 * @property {number} totalScore
 * @property {string} generatedAt
 * @property {Array<SuspicionFindingDto>} findings
 */
/**
 * @typedef {Object} SuspicionFindingDto
 * @property {string} ruleId
 * @property {string} rule
 * @property {number} scoreContribution
 * @property {string} detail
 * @property {(Object<string, number>|null)} metrics
 */
/**
 * @typedef {Object} HeatmapResponse
 * @property {string} matchId
 * @property {number} cellSizeMeters
 * @property {number} gridWidth
 * @property {Array<HeatmapCellDto>} cells
 */
/**
 * @typedef {Object} HeatmapCellDto
 * @property {number} gridX
 * @property {number} gridZ
 * @property {number} deaths
 * @property {number} landings
 */
/**
 * @typedef {Object} WeaponBalanceReportDto
 * @property {string} scope
 * @property {Array<WeaponBalanceItemDto>} weapons
 */
/**
 * @typedef {Object} WeaponBalanceItemDto
 * @property {string} weaponId
 * @property {number} kills
 * @property {number} deaths
 * @property {number} kd
 * @property {number} averageKillDistance
 * @property {number} averageTtkMs
 * @property {number} hitRate
 * @property {number} shots
 * @property {number} hits
 * @property {Array<number>} ttkPercentiles
 */
/**
 * @typedef {Object} RiskSeriesDto
 * @property {string} playerId
 * @property {Array<RiskPointDto>} points
 */
/**
 * @typedef {Object} RiskPointDto
 * @property {string} timestamp
 * @property {number} score
 * @property {(string|null)} matchId
 * @property {(string|null)} reason
 */
/**
 * @typedef {Object} ReviewQueueItemDto
 * @property {string} id
 * @property {string} playerId
 * @property {string} matchId
 * @property {number} riskScore
 * @property {string} enqueuedAt
 * @property {string} status
 * @property {(string|null)} notes
 */
/**
 * @typedef {Object} UpdateReviewStatusRequest
 * @property {number} status
 * @property {(string|null)} notes
 */
/**
 * @typedef {Object} ReplaySaveRequest
 * @property {string} matchId
 * @property {string} mapId
 * @property {(string|null)} startedAt
 * @property {Array<ReplayFrameDto>} frames
 */
/**
 * @typedef {Object} ReplayFrameDto
 * @property {number} sequence
 * @property {number} timeSeconds
 * @property {string} playerId
 * @property {number} x
 * @property {number} y
 * @property {number} z
 * @property {number} yaw
 * @property {number} pitch
 * @property {(number|null)} eventHint
 * @property {(string|null)} weaponId
 */
/**
 * @typedef {Object} ReplayMetaDto
 * @property {string} matchId
 * @property {string} formatVersion
 * @property {number} frameCount
 * @property {number} compressedBytes
 */
/**
 * @typedef {Object} ClientErrorRequest
 * @property {(string|null)} id
 * @property {(string|null)} trigger
 * @property {(string|null)} version
 * @property {(string|null)} scene
 * @property {(string|null)} platform
 * @property {(string|null)} deviceModel
 * @property {(string|null)} operatingSystem
 * @property {(string|null)} processorType
 * @property {number} processorCount
 * @property {number} systemMemoryMb
 * @property {(string|null)} graphicsDeviceName
 * @property {number} graphicsMemoryMb
 * @property {(string|null)} unityVersion
 * @property {(string|null)} exceptionType
 * @property {(string|null)} message
 * @property {(string|null)} stackTrace
 * @property {(Array<string>|null)} recentLogs
 * @property {(string|null)} createdAtUtc
 */
/**
 * @typedef {Object} ClientErrorDto
 * @property {string} id
 * @property {string} trigger
 * @property {string} version
 * @property {string} scene
 * @property {string} platform
 * @property {string} deviceModel
 * @property {string} operatingSystem
 * @property {string} processorType
 * @property {number} processorCount
 * @property {number} systemMemoryMb
 * @property {string} graphicsDeviceName
 * @property {number} graphicsMemoryMb
 * @property {string} unityVersion
 * @property {string} exceptionType
 * @property {string} message
 * @property {string} stackTrace
 * @property {Array<string>} recentLogs
 * @property {(string|null)} clientIp
 * @property {string} createdAt
 */
/**
 * @typedef {Object} ClientErrorListResponse
 * @property {Array<ClientErrorDto>} items
 * @property {number} total
 */
export {};
