import express from 'express';
import chalk from 'chalk';
import { QuickDB, JSONDriver } from 'quick.db';
import { uuid } from 'uuidv4';

// Initialize database and server
const db = new QuickDB({ driver: new JSONDriver() });
const app = express();
const port = 3000;

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

// Routes
app.get('/', (req, res) => {
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
            res.status(404).send('No fart data found');
        }
    } catch (err) {
        logError(`Error retrieving fart data for ${hwid}`, err.message);
        res.status(500).send('Internal server error');
    }
});

// Fart types (could be moved to a separate config file if needed)
const fartTypes = {
    wet: 'Wet',
    explosive: 'Explosive',
    classic: 'Classic',
    explosion: 'Explosion',
    wetlong: 'Wetlong',
    drylong: 'Drylong',
    puke: 'Puke',
    miniexplosion: 'miniexplosion',
};

// Start the server
app.listen(port, () => {
    logSuccess(`Server is running at ${chalk.underline(`http://localhost:${port}`)}`);
    db.push('clients.CAC7-29AF-E897-3AFB-B37F-967E-E35C-4C0A.farts', {
        //id: uuid(),
        type: fartTypes.classic,
        timestamp: Math.round(Date.now() / 1000),
    }).then(() => {
        console.log(chalk.green.bold('Fart data pushed'));
    }).catch(err => {
        console.error(chalk.red.bold(`Error pushing fart data: ${err}`));
    });
});