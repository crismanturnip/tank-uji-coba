using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

namespace TANCOUNTER01;

public class TANCOUNTER01 : Bot
{
    private const double TooCloseDistance = 180;
    private const double IdealMinDistance = 260;
    private const double IdealMaxDistance = 430;
    private const double FarDistance = 560;
    private const double WallMargin = 90;
    private const double LowEnergyThreshold = 22;
    private const double RadarSweepDegrees = 360;
    private const double RadarLockOvershoot = 1.7;
    private const int RadarLostTurnLimit = 14;
    private const int DirectionChangeInterval = 31;

    private readonly Random random = new Random();
    private EnemySnapshot target;
    private int moveDirection = 1;
    private int lastDirectionChangeTurn;

    public static void Main(string[] args)
    {
        new TANCOUNTER01().Start();
    }

    private TANCOUNTER01() : base(BotInfo.FromFile("TANCOUNTER01.json")) { }

    public override void Run()
    {
        ConfigureIdentity();

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        MaxSpeed = 7.5;
        GunTurnRate = 20;
        RadarTurnRate = 45;

        while (IsRunning)
        {
            ForgetLostTarget();
            ControlRadar();
            MoveGreedily();
            FireGreedily();
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Lock radar data: setiap scan menyimpan kondisi lokal musuh yang dipakai
        // heuristic greedy pada turn ini, bukan target score jangka panjang.
        target = new EnemySnapshot
        {
            Id = e.ScannedBotId,
            X = e.X,
            Y = e.Y,
            Bearing = BearingTo(e.X, e.Y),
            Distance = DistanceTo(e.X, e.Y),
            Energy = e.Energy,
            Speed = e.Speed,
            Direction = e.Direction,
            LastSeenTurn = TurnNumber
        };

        LockRadarToTarget();
        MoveGreedily();
        FireGreedily();
        SetRescan();
    }

    public override void OnHitWall(HitWallEvent e)
    {
        ReverseMovement();
        TurnTowardArenaCenter();
        TargetSpeed = 7;
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        ReverseMovement();
        TargetSpeed = Energy < LowEnergyThreshold ? 8 : 7;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        ReverseMovement();
        TurnTowardArenaCenter();

        if (GunHeat == 0 && Energy > 1.4)
        {
            SetTurnGunLeft(GunBearingTo(e.X, e.Y));
            SetFire(Energy < LowEnergyThreshold ? 0.8 : 1.4);
        }
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (target?.Id == e.VictimId)
        {
            target = null;
        }
    }

    private void ConfigureIdentity()
    {
        BodyColor = Color.FromArgb(24, 70, 77);
        TurretColor = Color.FromArgb(230, 165, 64);
        RadarColor = Color.FromArgb(115, 225, 202);
        GunColor = Color.FromArgb(235, 241, 245);
        BulletColor = Color.FromArgb(255, 220, 93);
        ScanColor = Color.FromArgb(128, 240, 213);
        TracksColor = Color.FromArgb(18, 32, 36);
    }

    private void ControlRadar()
    {
        if (target == null || IsTargetLost())
        {
            // Radar sweep 360 derajat: ketika belum ada musuh atau target hilang
            // beberapa turn, aksi greedy terbaik adalah membeli informasi arena.
            SetTurnRadarRight(RadarSweepDegrees);
            return;
        }

        LockRadarToTarget();
    }

    private void LockRadarToTarget()
    {
        if (target == null)
        {
            return;
        }

        // Lock radar: radar diarahkan melewati posisi target sedikit agar scan
        // tetap stabil walaupun musuh bergerak di antara dua turn.
        var radarBearing = RadarBearingTo(target.X, target.Y);
        SetTurnRadarLeft(radarBearing * RadarLockOvershoot);
    }

    private void MoveGreedily()
    {
        // Hubungan ke fungsi objektif skor: setiap turn bot memaksimalkan peluang
        // skor lokal dengan urutan safety dulu (hindari wall damage dan ramming),
        // lalu menjaga jarak ideal untuk bullet hit, lalu mendekat jika informasi
        // target terlalu jauh untuk counter attack efektif.
        if (IsNearWall())
        {
            EscapeWallGreedily();
            return;
        }

        if (target == null || IsTargetLost())
        {
            PatrolWhileSearching();
            return;
        }

        if (TurnNumber - lastDirectionChangeTurn > DirectionChangeInterval + random.Next(16))
        {
            ReverseMovement();
        }

        if (target.Distance < TooCloseDistance)
        {
            MoveDiagonalAwayFromTarget();
        }
        else if (target.Distance <= IdealMaxDistance)
        {
            CircleAtSafeDistance();
        }
        else
        {
            ApproachSafely();
        }
    }

    private void EscapeWallGreedily()
    {
        // Greedy movement: dinding punya prioritas tertinggi karena wall damage
        // langsung mengurangi survival dan skor. Arah keluar dipilih ke tengah arena.
        TurnTowardArenaCenter();
        TargetSpeed = Energy < LowEnergyThreshold ? 8 : 7;
    }

    private void PatrolWhileSearching()
    {
        SetTurnLeft(38 * moveDirection);
        TargetSpeed = 5.5;
    }

    private void MoveDiagonalAwayFromTarget()
    {
        if (target == null)
        {
            return;
        }

        // Greedy movement: musuh terlalu dekat dianggap bahaya, jadi bot memilih
        // arah diagonal menjauh, bukan mundur lurus, agar lebih sulit diprediksi.
        var enemyDirection = DirectionTo(target.X, target.Y);
        var desiredDirection = NormalizeAbsoluteAngle(enemyDirection + 135 * moveDirection);

        if (WouldEndNearWall(desiredDirection, 130))
        {
            desiredDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        }

        SetTurnLeft(CalcDeltaAngle(desiredDirection, Direction));
        TargetSpeed = 8;
    }

    private void CircleAtSafeDistance()
    {
        if (target == null)
        {
            return;
        }

        // Greedy movement: jarak menengah adalah zona counter attack, sehingga
        // bot bergerak menyamping/circling untuk mempertahankan lock sambil sulit ditembak.
        var enemyDirection = DirectionTo(target.X, target.Y);
        var strafeOffset = target.Distance < IdealMinDistance ? 115 : 90;
        var desiredDirection = NormalizeAbsoluteAngle(enemyDirection + strafeOffset * moveDirection);

        SetTurnLeft(CalcDeltaAngle(desiredDirection, Direction));
        TargetSpeed = Energy < LowEnergyThreshold ? 7.5 : 6.5;
    }

    private void ApproachSafely()
    {
        if (target == null)
        {
            return;
        }

        // Greedy movement: target jauh didekati secara miring, bukan lurus,
        // untuk mengurangi risiko terkena tembakan linear.
        var enemyDirection = DirectionTo(target.X, target.Y);
        var approachOffset = target.Distance > FarDistance ? 35 : 55;
        var desiredDirection = NormalizeAbsoluteAngle(enemyDirection + approachOffset * moveDirection);

        if (WouldEndNearWall(desiredDirection, 120))
        {
            desiredDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        }

        SetTurnLeft(CalcDeltaAngle(desiredDirection, Direction));
        TargetSpeed = Energy < LowEnergyThreshold ? 6 : 7;
    }

    private void FireGreedily()
    {
        if (target == null || IsTargetLost() || GunHeat > 0)
        {
            return;
        }

        var gunBearing = GunBearingTo(target.X, target.Y);
        SetTurnGunLeft(gunBearing);

        var firePower = ChooseFirePower();
        var stableLock = TurnNumber - target.LastSeenTurn <= 1;
        var aimTolerance = target.Distance < TooCloseDistance ? 8 : target.Distance < IdealMaxDistance ? 4.5 : 2.8;

        if (stableLock && Math.Abs(gunBearing) <= aimTolerance && Energy > firePower + 0.2)
        {
            SetFire(firePower);
        }
    }

    private double ChooseFirePower()
    {
        if (target == null)
        {
            return 0;
        }

        // Greedy fire power: pilih power lokal terbaik dari jarak, energi sendiri,
        // energi musuh, dan kualitas lock. Tembakan besar hanya dipakai saat target
        // masih segar agar energi tidak dibuang untuk scan yang sudah basi.
        var lockAge = TurnNumber - target.LastSeenTurn;
        var power = target.Distance < TooCloseDistance ? 2.0 :
                    target.Distance <= IdealMaxDistance ? 1.6 :
                    0.8;

        if (target.Energy < 12 && target.Distance < IdealMaxDistance && lockAge <= 1)
        {
            power = 2.4;
        }

        if (target.Distance > FarDistance || lockAge > 1)
        {
            power = Math.Min(power, 0.9);
        }

        if (Energy < LowEnergyThreshold)
        {
            power = Math.Min(power, 0.8);
        }

        return Math.Clamp(power, 0.2, Math.Min(3.0, Energy - 0.1));
    }

    private void ForgetLostTarget()
    {
        if (target != null && IsTargetLost())
        {
            target = null;
        }
    }

    private bool IsTargetLost()
    {
        return target != null && TurnNumber - target.LastSeenTurn > RadarLostTurnLimit;
    }

    private bool IsNearWall()
    {
        return X < WallMargin ||
               X > ArenaWidth - WallMargin ||
               Y < WallMargin ||
               Y > ArenaHeight - WallMargin;
    }

    private bool WouldEndNearWall(double direction, double distance)
    {
        var radians = direction * Math.PI / 180;
        var nextX = X + Math.Cos(radians) * distance;
        var nextY = Y + Math.Sin(radians) * distance;

        return nextX < WallMargin ||
               nextX > ArenaWidth - WallMargin ||
               nextY < WallMargin ||
               nextY > ArenaHeight - WallMargin;
    }

    private void TurnTowardArenaCenter()
    {
        var centerDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        SetTurnLeft(CalcDeltaAngle(centerDirection, Direction));
    }

    private void ReverseMovement()
    {
        moveDirection *= -1;
        lastDirectionChangeTurn = TurnNumber;
    }

    private sealed class EnemySnapshot
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Bearing { get; set; }
        public double Distance { get; set; }
        public double Energy { get; set; }
        public double Speed { get; set; }
        public double Direction { get; set; }
        public int LastSeenTurn { get; set; }
    }
}
