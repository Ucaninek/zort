import express from 'express';
import chalk from 'chalk';
import { QuickDB, JSONDriver } from 'quick.db';
import { v4 as uuid } from 'uuid';
import crypto from 'crypto';
import cookieParser from 'cookie-parser';
import dotenv from 'dotenv';
import rateLimit from 'express-rate-limit';
import path from 'path';
import fs from 'fs';
import https from 'https';
import { time } from 'console';
dotenv.config();

// Initialize database and server
const db = new QuickDB({ driver: new JSONDriver() });
const app = express();
const port = process.env.PORT || 3000;

app.use(express.static('public'));

app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public/index.html'));
});

const authLimiter = rateLimit({
    windowMs: 60 * 1000, //1 minute
    max: 5, // limit each IP to 5 requests per windowMs
    skipSuccessfulRequests: true,
    message: 'Too many login attempts, try again later.',
});
app.use('/auth', authLimiter);

app.use(cookieParser());

const fartTypes = {
    wet: 'Wet',
    explosive: 'Explosive',
    classic: 'Classic',
    wetlong: 'Wetlong',
    drylong: 'Drylong',
    puke: 'Puke',
    miniexplosion: 'Miniexplosion',
};

const isOnline = (heartbeat) => {
    const lastHeartbeat = heartbeat || 0;
    const currentTime = Math.round(Date.now());
    const timeDiff = currentTime - lastHeartbeat;
    const threshold = 60 * 1000; // 1 minute
    return timeDiff < threshold;
};

// Utility function for logging errors
const logError = (message, details = '') => {
    console.error(chalk.red.bold(`${message}${details ? `: ${details}` : ''}`));
};

// Utility function for logging success
const logSuccess = (message) => {
    console.log(chalk.green.bold(message));
};

// Utility function for logging info
const logInfo = (message) => {
    console.log(chalk.blue.bold(message));
};

const isAuthenticated = (req) => {
    const sessionToken = req.cookies.session;
    if (!sessionToken) {
        return false;
    }

    const sessionData = db.get(`sessions.${sessionToken}`);
    if (!sessionData || sessionData.expires < Date.now()) {
        return false;
    }

    return true;
};

// Middleware for authentication
const authMiddleware = async (req, res, next) => {
    if (!(await isAuthenticated(req))) {
        logError('Unauthorized access');
        return res.status(401).send('Unauthorized');
    }
    next();
};

// Routes
app.post('/', (req, res) => {
    res.send('Hey boyz!!');
});

app.get('/log', async (req, res) => {
    const { hwid, sysdata } = req.query;

    if (!hwid || !sysdata) {
        logError('Log failed', `hwid and sysdata are required, hwid: ${hwid}, sysdata: ${sysdata}`);
        return res.status(400).send('hwid and sysdata are required');
    }

    try {
        await db.set(`clients.${hwid}.sysdata`, JSON.parse(sysdata));
        logSuccess(`Data stored for ${hwid}`);
        res.send('OK');
    } catch (err) {
        logError(`Error storing data for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/heartbeat', async (req, res) => {
    const { hwid } = req.query;

    if (!hwid) {
        logError('Heartbeat failed', 'hwid is required');
        return res.status(400).send('hwid is required');
    }

    try {
        await db.set(`clients.${hwid}.heartbeat`, Date.now());
        logInfo(`Heartbeat received from ${hwid}`);
        res.send('OK');
    } catch (err) {
        logError(`Error logging heartbeat for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/getFarts', async (req, res) => {
    const { hwid } = req.query;

    if (!hwid) {
        return res.status(400).send('hwid is required');
    }

    try {
        const farts = await db.get(`clients.${hwid}.farts`);
        if (farts) {
            logInfo(`Fart data sent to ${hwid}`);
            var fartData = farts.filter(fart => {
                //filter out farts older then 1 minute
                return (Math.round(Date.now() / 1000) - fart.timestamp) < 60;
            });
            res.json({ farts: fartData });
        } else {
            res.status(200).json({ farts: [] });
        }
    } catch (err) {
        logError(`Error retrieving fart data for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/auth', async (req, res) => {
    const { username, password } = req.query;
    if (!username || !password) {
        logError('Auth failed', 'username and password are required');
        return res.status(400).send('username and password are required');
    }

    const hashPassword = (password) => {
        return crypto.createHash('sha256').update(password).digest('hex');
    };

    const generateSessionToken = () => {
        return crypto.randomBytes(64).toString('hex');
    };

    const storedUsername = process.env.ADMIN_USERNAME || 'bismillahirrah'; // Store securely in environment variables
    const storedPasswordHash = process.env.ADMIN_PASSWORD_HASH || hashPassword('amrahim'); // Store securely in environment variables

    if (username === storedUsername && hashPassword(password) === storedPasswordHash) {
        const sessionToken = generateSessionToken();
        // Store session token in the database (or any other secure storage)
        await db.set(`sessions.${sessionToken}`, { username, expires: Date.now() + 24 * 60 * 60 * 1000 }); // 24 hours expiration

        // Set session cookie with secure attributes
        res.cookie('session', sessionToken, { maxAge: 24 * 60 * 60 * 1000, httpOnly: true, secure: process.env.NODE_ENV === 'production', sameSite: 'Strict' });

        logSuccess('Admin authenticated successfully');
        return res.send('OK');
    } else {
        logError('Auth failed', 'Invalid username or password');
        return res.status(401).send('Invalid username or password');
    }
});

app.get('/getClients', authMiddleware, async (req, res) => {
    try {
        var clients = await db.get('clients');
        if (clients) {
            // get request ip
            const ip = req.headers['x-forwarded-for'] || req.socket.remoteAddress;
            logInfo('Client data sent to admin from ' + ip);
            //add online status to each client
            for (const hwid in clients) {
                clients[hwid].isOnline = isOnline(clients[hwid].heartbeat);
            }
            res.json(clients);
        } else {
            res.status(404).send('No client data found');
        }
    } catch (err) {
        logError('Error retrieving client data', err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/removeFart', authMiddleware, async (req, res) => {
    const { hwid, fartId } = req.query;

    if (!hwid || !fartId) {
        logError('Remove fart failed', 'hwid and fartId are required');
        return res.status(400).send('hwid and fartId are required');
    }

    try {
        const farts = await db.get(`clients.${hwid}.farts`);
        if (farts) {
            const updatedFarts = farts.filter(fart => fart.id !== fartId);
            await db.set(`clients.${hwid}.farts`, updatedFarts);
            logSuccess(`Fart ${fartId} removed for ${hwid}`);
            res.send('OK');
        } else {
            res.status(404).send('No fart data found');
        }
    } catch (err) {
        logError(`Error removing fart data for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/clearFarts', authMiddleware, async (req, res) => {
    const { hwid } = req.query;

    if (!hwid) {
        logError('Clear farts failed', 'hwid is required');
        return res.status(400).send('hwid is required');
    }

    try {
        await db.delete(`clients.${hwid}.farts`);
        logSuccess(`All farts cleared for ${hwid}`);
        res.send('OK');
    } catch (err) {
        logError(`Error clearing fart data for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/logout', authMiddleware, async (req, res) => {
    const sessionToken = req.cookies.session;
    if (sessionToken) {
        await db.delete(`sessions.${sessionToken}`);
        res.clearCookie('session');
        logSuccess('Admin logged out successfully');
        return res.send('OK');
    } else {
        logError('Logout failed', 'No session token found');
        return res.status(400).send('No session token found');
    }
});

app.get('/scheduleFart', authMiddleware, async (req, res) => {
    const { hwid, type, timestamp } = req.query;

    if (!hwid || !type) {
        logError('Schedule fart failed', 'hwid and type are required');
        return res.status(400).send('hwid and type are required');
    }

    // Check if the hwid is valid
    const client = await db.get(`clients.${hwid}`);
    if (!client) {
        logError('Fart now failed', `Invalid hwid: ${hwid}`);
        return res.status(400).send('Invalid hwid');
    }

    try {
        const fartData = {
            id: uuid(),
            type: fartTypes[type] || fartTypes.classic,
            timestamp: Math.round(timestamp / 1000),
        };
        await db.push(`clients.${hwid}.farts`, fartData);
        logError(`Fart scheduled for ${hwid}`, `Type: ${fartData.type}, Timestamp: ${new Date(Math.round(timestamp)).toLocaleString()}, raw: ${timestamp}`);
        logSuccess(`Fart scheduled for ${hwid}`);
        res.send(`Fart scheduled for ${hwid} at ${new Date(Math.round(timestamp)).toLocaleString()}`);
    } catch (err) {
        logError(`Error scheduling fart for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

app.get('/fartNow', authMiddleware, async (req, res) => {
    let { hwid, type } = req.query;

    if (!hwid) {
        logError('Fart now failed', 'hwid is required');
        return res.status(400).send('hwid is required');
    }

    if (!type) type = fartTypes.classic;

    // Check if the type is valid
    if (!fartTypes[type]) {
        logError('Fart now failed', `Invalid fart type: ${type}`);
        return res.status(400).send('Invalid fart type');
    }

    // Check if the hwid is valid
    const client = await db.get(`clients.${hwid}`);
    if (!client) {
        logError('Fart now failed', `Invalid hwid: ${hwid}`);
        return res.status(400).send('Invalid hwid');
    }

    try {
        const fartData = {
            id: uuid(),
            type: type,
            timestamp: Math.round(Date.now() / 1000),
        };
        await db.push(`clients.${hwid}.farts`, fartData);
        logSuccess(`Fart now triggered for ${hwid}`);
        res.send(`Fart now triggered for ${hwid} at ${new Date().toLocaleString()}`);
    } catch (err) {
        logError(`Error triggering fart now for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

// Start the server
//app.listen(3000, () => {
//    logSuccess(`Server is running at ${chalk.underline(`http://localhost:${port}`)}`);
//});

https.createServer({
    key: fs.readFileSync("cert/key.pem"),
    cert: fs.readFileSync("cert/cert.pem")
}, app).listen(port, () => {
    logSuccess(`Server is running at ${chalk.underline(`http://localhost:${port}`)}`);
});